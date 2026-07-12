@echo off
REM Khoi dong sidecar embedding local cho module RBL (e5 / PhoBERT / bge-m3).
REM Lan dau se tu tai model ve (~4GB). Giu cua so nay mo khi dung benchmark.
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
set EMBED_PORT=8600
cd /d "%~dp0.."
python tools\embedding_server.py
pause
