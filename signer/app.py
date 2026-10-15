"""Local EVM signer for BMONI's raw-digest and EIP-191 owner-proof flows."""

from __future__ import annotations

import base64
import json
import os
import secrets
import tempfile
import threading
from pathlib import Path
from typing import Annotated

from eth_account import Account
from eth_account.messages import encode_defunct
from fastapi import FastAPI, Header, HTTPException
from pydantic import BaseModel, Field
from cryptography.hazmat.primitives.ciphers.aead import AESGCM


class SignerError(RuntimeError):
    pass


class UserKeyBody(BaseModel):
    userKey: str = Field(min_length=1, max_length=255)


class HashBody(UserKeyBody):
    hash: str = Field(pattern=r"^0x[0-9a-fA-F]{64}$")


class MessageBody(UserKeyBody):
    message: str = Field(min_length=1, max_length=4096)


class EncryptedKeyStore:
    """JSON key index whose private keys are AES-256-GCM encrypted at rest."""

    def __init__(self, path: Path, master_key_hex: str):
        try:
            key = bytes.fromhex(master_key_hex.removeprefix("0x"))
        except ValueError as exc:
            raise SignerError("BMONI_MASTER_KEY must be 32-byte hex") from exc
        if len(key) != 32:
            raise SignerError("BMONI_MASTER_KEY must be 32-byte hex")
        self.path = path
        self._cipher = AESGCM(key)
        self._lock = threading.RLock()

    @staticmethod
    def _aad(user_key: str, owner_address: str) -> bytes:
        return ("bmoni-signer:v1:" + user_key + ":" + owner_address).encode("utf-8")

    def _read(self) -> dict[str, dict[str, str]]:
        if not self.path.exists():
            return {}
        return json.loads(self.path.read_text(encoding="utf-8"))

    def _write(self, records: dict[str, dict[str, str]]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        fd, tmp_name = tempfile.mkstemp(prefix="signer-", suffix=".tmp", dir=self.path.parent)
        try:
            if os.name != "nt":
                os.fchmod(fd, 0o600)
            with os.fdopen(fd, "w", encoding="utf-8") as handle:
                json.dump(records, handle, separators=(",", ":"))
                handle.flush()
                os.fsync(handle.fileno())
            os.replace(tmp_name, self.path)
            if os.name != "nt":
                self.path.chmod(0o600)
        finally:
            if os.path.exists(tmp_name):
                os.unlink(tmp_name)

    def _private_key(self, record: dict[str, str], user_key: str) -> bytes:
        nonce = base64.b64decode(record["nonce"])
        ciphertext = base64.b64decode(record["ciphertext"])
        return self._cipher.decrypt(nonce, ciphertext, self._aad(user_key, record["ownerAddress"]))

    def init(self, user_key: str) -> str:
        with self._lock:
            records = self._read()
            record = records.get(user_key)
            if record is not None:
                return record["ownerAddress"]
            account = Account.create()
            private_key = bytes(account.key)
            nonce = secrets.token_bytes(12)
            ciphertext = self._cipher.encrypt(nonce, private_key, self._aad(user_key, account.address))
            records[user_key] = {
                "ownerAddress": account.address,
                "nonce": base64.b64encode(nonce).decode("ascii"),
                "ciphertext": base64.b64encode(ciphertext).decode("ascii"),
            }
            self._write(records)
            return account.address

    def sign_hash(self, user_key: str, digest: bytes) -> str:
        with self._lock:
            record = self._read().get(user_key)
            if record is None:
                raise KeyError(user_key)
            private_key = self._private_key(record, user_key)
            signed = Account.unsafe_sign_hash(digest, private_key=private_key)
            if Account._recover_hash(digest, signature=signed.signature) != record["ownerAddress"]:
                raise SignerError("Signature recovery did not match the stored owner")
            return signed.signature.hex()

    def sign_message(self, user_key: str, message: str) -> str:
        with self._lock:
            record = self._read().get(user_key)
            if record is None:
                raise KeyError(user_key)
            private_key = self._private_key(record, user_key)
            encoded_message = encode_defunct(text=message)
            signed = Account.sign_message(encoded_message, private_key=private_key)
            if Account.recover_message(encoded_message, signature=signed.signature) != record["ownerAddress"]:
                raise SignerError("Signature recovery did not match the stored owner")
            return signed.signature.hex()


def create_app(store: EncryptedKeyStore | None = None, secret: str | None = None) -> FastAPI:
    configured_secret = secret if secret is not None else os.environ.get("SIGNER_SECRET", "")
    if not configured_secret:
        raise SignerError("SIGNER_SECRET must be configured")
    if len(configured_secret) < 32:
        raise SignerError("SIGNER_SECRET must contain at least 32 characters")
    if store is None:
        master_key = os.environ.get("BMONI_MASTER_KEY", "")
        if not master_key:
            raise SignerError("BMONI_MASTER_KEY must be configured")
        store = EncryptedKeyStore(Path(os.environ.get("SIGNER_KEYS_PATH", "/data/signer-keys.json")), master_key)

    app = FastAPI(title="BMONI local signer", docs_url=None, redoc_url=None)

    def authenticate(provided: str | None) -> None:
        if provided is None or not secrets.compare_digest(provided, configured_secret):
            raise HTTPException(status_code=401, detail="Unauthorized")

    @app.get("/health")
    def health():
        return {"status": "ok"}

    @app.post("/wallets/init")
    def init_wallet(body: UserKeyBody, x_signer_secret: Annotated[str | None, Header()] = None):
        authenticate(x_signer_secret)
        return {"ownerAddress": store.init(body.userKey)}

    @app.post("/sign-hash")
    def sign_hash(body: HashBody, x_signer_secret: Annotated[str | None, Header()] = None):
        authenticate(x_signer_secret)
        try:
            signature = store.sign_hash(body.userKey, bytes.fromhex(body.hash[2:]))
        except KeyError:
            raise HTTPException(status_code=404, detail="Signer wallet not initialized") from None
        return {"signature": signature}

    @app.post("/sign-message")
    def sign_message(body: MessageBody, x_signer_secret: Annotated[str | None, Header()] = None):
        authenticate(x_signer_secret)
        try:
            signature = store.sign_message(body.userKey, body.message)
        except KeyError:
            raise HTTPException(status_code=404, detail="Signer wallet not initialized") from None
        return {"signature": signature}

    return app


app = create_app()
