FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    SIGNER_KEYS_PATH=/data/signer-keys.json

WORKDIR /app
COPY signer/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt
COPY signer/app.py ./app.py
RUN mkdir -p /data && chmod 700 /data

EXPOSE 8090
CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8090", "--no-server-header"]
