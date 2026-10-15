# Local BMONI signer

The signer keeps generated owner keys encrypted with AES-256-GCM in `/data/signer-keys.json`. Set `BMONI_MASTER_KEY` to a securely generated 32-byte hex value and `SIGNER_SECRET` to a separate random secret. Keep both in a local secret store or environment; never commit them. Mount `/data` on a persistent volume and restrict access to it.

Build and run with Docker:

```powershell
docker build -t bmoni-signer .
docker run --rm -p 8090:8090 `
  -e BMONI_MASTER_KEY `
  -e SIGNER_SECRET `
  -v bmoni-signer-data:/data `
  bmoni-signer
```

The endpoints require `x-signer-secret`. `POST /sign-message` uses EIP-191 personal signing; `POST /sign-hash` signs exactly 32 raw bytes. `POST /wallets/init` creates a wallet once per `userKey` and returns its owner address.

Run tests from this directory with `python -m pip install -r requirements.txt` and `python -m pytest`. The documented signing vector test reads `BMONI_SIGNING_VECTOR_PRIVATE_KEY` from the process environment so that the vector key is not stored in this repository.
