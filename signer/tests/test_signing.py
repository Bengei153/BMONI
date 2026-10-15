import json
import os
import asyncio

from eth_account import Account
from eth_account.messages import encode_defunct
from eth_utils import keccak
from httpx import ASGITransport, AsyncClient

os.environ.setdefault("SIGNER_SECRET", "test-only-signer-secret-32-characters")
os.environ.setdefault("BMONI_MASTER_KEY", "33" * 32)

from app import EncryptedKeyStore, create_app


TEST_MASTER_KEY = "11" * 32
KNOWN_PRIVATE_KEY = os.environ.get("BMONI_SIGNING_VECTOR_PRIVATE_KEY")
KNOWN_HASH = "0x8f5156823a5c2cdc7bedc12253e49e4946c6fff0273034eb485750035d21ad31"
KNOWN_SIGNATURE = (
    "0x628f1aff48c9d1f35d45a735eb026db0437c5ed334a94dc7fb0ac86ca32c10bd"
    "173a653a7f064c4512244f6fcbefb07e13bfe7368fcacdcc4e6fb153f50050991b"
)
KNOWN_OWNER = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266"
KNOWN_PREIMAGE = "bmoni-embedded:BKE-2041:sign-payload-example"


def test_raw_digest_matches_bmoni_documented_vector_and_recovers_owner():
    digest = bytes.fromhex(KNOWN_HASH[2:])
    assert "0x" + keccak(text=KNOWN_PREIMAGE).hex() == KNOWN_HASH
    assert Account._recover_hash(digest, signature=KNOWN_SIGNATURE) == KNOWN_OWNER
    if KNOWN_PRIVATE_KEY:
        account = Account.from_key(KNOWN_PRIVATE_KEY)
        assert account.address == KNOWN_OWNER
        signature = Account.unsafe_sign_hash(digest, private_key=account.key).signature.hex()
        assert signature == KNOWN_SIGNATURE


def test_persisted_signer_uses_raw_digest_and_encrypted_storage(tmp_path):
    path = tmp_path / "keys.json"
    store = EncryptedKeyStore(path, TEST_MASTER_KEY)
    owner = store.init("local-user-1")
    digest = bytes.fromhex(KNOWN_HASH[2:])

    signature = store.sign_hash("local-user-1", digest)

    assert len(signature.removeprefix("0x")) == 130
    assert Account._recover_hash(digest, signature=signature) == owner
    serialized = path.read_text(encoding="utf-8")
    assert KNOWN_PRIVATE_KEY is None or KNOWN_PRIVATE_KEY not in serialized
    assert "private_key" not in json.loads(serialized)["local-user-1"]
    reopened = EncryptedKeyStore(path, TEST_MASTER_KEY)
    assert reopened.init("local-user-1") == owner
    assert Account._recover_hash(digest, signature=reopened.sign_hash("local-user-1", digest)) == owner


def test_sign_message_uses_eip191_personal_sign_prefix(tmp_path):
    store = EncryptedKeyStore(tmp_path / "keys.json", TEST_MASTER_KEY)
    owner = store.init("local-user-2")
    message = "BMONI owner proof challenge"

    signature = store.sign_message("local-user-2", message)

    recovered = Account.recover_message(encode_defunct(text=message), signature=signature)
    assert recovered == owner


def test_wrong_master_key_cannot_decrypt_persisted_signer(tmp_path):
    path = tmp_path / "keys.json"
    EncryptedKeyStore(path, TEST_MASTER_KEY).init("local-user-3")
    wrong_store = EncryptedKeyStore(path, "22" * 32)

    try:
        wrong_store.sign_hash("local-user-3", bytes.fromhex(KNOWN_HASH[2:]))
    except Exception as exc:
        assert type(exc).__name__ == "InvalidTag"
    else:
        raise AssertionError("Expected AES-GCM decryption to reject an incorrect master key")


def test_http_sign_hash_endpoint_returns_raw_signature_and_requires_secret(tmp_path):
    owner_store = EncryptedKeyStore(tmp_path / "keys.json", TEST_MASTER_KEY)
    owner = owner_store.init("api-user")
    app = create_app(owner_store, secret="route-secret-test-secret-32-characters")
    payload = {"userKey": "api-user", "hash": KNOWN_HASH}

    async def invoke():
        async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
            unauthorized = await client.post("/sign-hash", json=payload)
            authorized = await client.post("/sign-hash", json=payload,
                headers={"x-signer-secret": "route-secret-test-secret-32-characters"})
            return unauthorized, authorized

    unauthorized, response = asyncio.run(invoke())

    assert unauthorized.status_code == 401
    assert response.status_code == 200
    signature = response.json()["signature"]
    assert len(signature.removeprefix("0x")) == 130
    assert Account._recover_hash(bytes.fromhex(KNOWN_HASH[2:]), signature=signature) == owner


def test_http_sign_message_endpoint_recovers_owner_with_eip191(tmp_path):
    owner_store = EncryptedKeyStore(tmp_path / "keys.json", TEST_MASTER_KEY)
    owner = owner_store.init("api-user")
    app = create_app(owner_store, secret="route-secret-test-secret-32-characters")
    message = "owner proof challenge"

    async def invoke():
        async with AsyncClient(transport=ASGITransport(app=app), base_url="http://test") as client:
            return await client.post("/sign-message", json={"userKey": "api-user", "message": message},
                headers={"x-signer-secret": "route-secret-test-secret-32-characters"})

    response = asyncio.run(invoke())

    assert response.status_code == 200
    signature = response.json()["signature"]
    assert Account.recover_message(encode_defunct(text=message), signature=signature) == owner
