"""
Local embedding sidecar cho module RBL của ChatBot PRN222.

Chạy 3 embedding model MÃ NGUỒN MỞ trong đề hoàn toàn offline (không cần API key):
  - multilingual-e5-base : intfloat/multilingual-e5-base   (đa ngữ)
  - PhoBERT-base         : vinai/phobert-base               (tiếng Việt, MLM -> mean pooling)
  - bge-m3               : BAAI/bge-m3                      (đa ngữ, nặng ~2GB)

Backend .NET (LocalStEmbeddingProvider) gọi POST /embed {model, text} -> {vector:[...]}.
Model được nạp lười (chỉ tải khi lần đầu benchmark model đó).

Chạy:  python tools/embedding_server.py      (mặc định cổng 8600)
Cài:   pip install flask sentence-transformers torch --index-url https://download.pytorch.org/whl/cpu
"""
import os
import sys

# Console Windows mặc định cp1252 -> ép UTF-8 để log không vỡ.
try:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
except Exception:
    pass

from flask import Flask, request, jsonify

MODEL_MAP = {
    "multilingual-e5-base": "intfloat/multilingual-e5-base",
    "PhoBERT-base": "vinai/phobert-base",
    "bge-m3": "BAAI/bge-m3",
}

app = Flask(__name__)
_loaded = {}          # model_id -> SentenceTransformer
_SentenceTransformer = None


def _get_st():
    global _SentenceTransformer
    if _SentenceTransformer is None:
        from sentence_transformers import SentenceTransformer  # nạp lười để khởi động nhanh
        _SentenceTransformer = SentenceTransformer
    return _SentenceTransformer


def _load(model_id: str):
    if model_id in _loaded:
        return _loaded[model_id]
    hf_name = MODEL_MAP.get(model_id)
    if not hf_name:
        raise ValueError(f"Model không hỗ trợ: {model_id}")
    print(f"[embedding_server] loading {model_id} ({hf_name}) ...", flush=True)
    ST = _get_st()
    # PhoBERT khong phai sentence-transformers -> ST tu boc Transformer + Pooling(mean).
    model = ST(hf_name, device="cpu")
    _loaded[model_id] = model
    print(f"[embedding_server] ready: {model_id}", flush=True)
    return model


@app.get("/health")
def health():
    return jsonify(status="ok", loaded=list(_loaded.keys()), supported=list(MODEL_MAP.keys()))


@app.post("/embed")
def embed():
    data = request.get_json(force=True) or {}
    model_id = data.get("model", "")
    text = data.get("text", "") or ""
    try:
        model = _load(model_id)
        vec = model.encode(text, normalize_embeddings=True, show_progress_bar=False)
        return jsonify(vector=[float(x) for x in vec], dim=int(len(vec)))
    except Exception as ex:  # noqa: BLE001
        return jsonify(error=str(ex)), 500


@app.post("/embed/batch")
def embed_batch():
    """Embed nhiều đoạn cùng lúc — nhanh hơn gọi /embed từng chunk."""
    data = request.get_json(force=True) or {}
    model_id = data.get("model", "")
    texts = data.get("texts") or []
    if not isinstance(texts, list):
        return jsonify(error="texts phải là mảng chuỗi"), 400
    try:
        model = _load(model_id)
        if len(texts) == 0:
            return jsonify(vectors=[], dim=0)
        vecs = model.encode(texts, normalize_embeddings=True, show_progress_bar=False)
        dim = int(len(vecs[0])) if len(vecs) > 0 else 0
        return jsonify(
            vectors=[[float(x) for x in v] for v in vecs],
            dim=dim,
        )
    except Exception as ex:  # noqa: BLE001
        return jsonify(error=str(ex)), 500


if __name__ == "__main__":
    port = int(os.environ.get("EMBED_PORT", "8600"))
    print(f"[embedding_server] listening http://127.0.0.1:{port}  (models: {', '.join(MODEL_MAP)})", flush=True)
    app.run(host="127.0.0.1", port=port, threaded=True)
