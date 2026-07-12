# ChatBot PRN222 — RAG Chatbot học tập + Module nghiên cứu (RBL)

Web app **chatbot hỏi–đáp tài liệu học tập** theo cơ chế **RAG (Retrieval-Augmented Generation)**: sinh viên hỏi, hệ thống truy xuất đúng đoạn tài liệu đã index rồi để LLM trả lời **có trích dẫn nguồn** và **chỉ trong phạm vi tài liệu**. Kèm một **Module nghiên cứu (RBL)** để benchmark RAG vs Fine-tuned, các chiến lược chunking và các model embedding.

- **Nền tảng:** ASP.NET Core 8 (Razor Pages) · kiến trúc 3 lớp (Presentation / Service / DataAccess)
- **CSDL:** Microsoft SQL Server (EF Core)
- **LLM:** Groq — `llama-3.3-70b-versatile` (OpenAI-compatible API)
- **Embedding:** local offline (feature-hashing) + sidecar Python `sentence-transformers` cho các model mở (e5 / bge-m3 / PhoBERT)
- **Khác:** SignalR realtime, Cookie Auth (Admin/Lecturer/Student), gói token + thanh toán mô phỏng, feedback, báo cáo doanh thu

---

## 1. Tính năng chính

### A. Quản lý tài liệu
- Upload **PDF / DOCX / PPTX** (slide bài giảng) — trích xuất text bằng PdfPig + OpenXML.
- **Tự động chunk & embed** ngay khi upload (chiến lược chunk + model embedding lấy theo Cài đặt RBL; vector lưu ở cột `DocumentChunks.VectorJson`).
- Quản lý theo **Môn học → Chương**.
- Xem danh sách tài liệu đã index + số chunk + trạng thái.

### B. Chat & Hỏi đáp
- Chat tự nhiên, **giữ ngữ cảnh hội thoại** (kèm các lượt trước).
- **Trích dẫn nguồn** tài liệu gốc (tên file, trang, độ tin cậy).
- **Grounded** — chỉ trả lời trong phạm vi tài liệu; ngoài phạm vi → *"Tôi không tìm thấy thông tin này trong tài liệu môn học."* (không kèm nguồn giả).
- **Lịch sử theo phiên** chat, tự đặt tiêu đề.

### C. Module nghiên cứu (RBL) — dành cho Admin/Lecturer
- **So sánh RAG vs Fine-tuned** (parametric): cùng câu hỏi, cùng base LLM — một nhánh có retrieval + citation, một nhánh trả lời từ kiến thức nội tại.
- **Benchmark chunking strategy**: Semantic (SK) / Fixed-Size / Sentence — đo retrieval score, số chunk, độ trễ.
- **Benchmark embedding model**: `Local (hashing)`, `multilingual-e5-base`, `PhoBERT-base`, `bge-m3` (chạy offline), `text-embedding-3-small` (cần OpenAI key) — đo cosine retrieval + độ trễ.
- **Dashboard thực nghiệm**: tổng hợp mọi lần chạy.

### D. Vận hành
- Phân quyền Admin / Lecturer / Student; cấp quyền upload theo môn cho giảng viên.
- Gói token (mô phỏng thanh toán) + hạn mức cho sinh viên.
- Feedback + trả lời, whitelist email, báo cáo token & doanh thu (VND).

---

## 2. Kiến trúc

```
PresentationLayer  (Razor Pages, Controllers API, SignalR Hubs, EmbeddingSidecarLauncher)
        │  gọi
ServiceLayer       (ChatService, DocumentService, ChunkingService, RetrievalService,
        │           GroqService, các *ComparisonService của RBL, BillingService, ReportService,
        │           Embeddings/{Local, LocalST, HuggingFace, OpenAI}EmbeddingProvider)
        │  gọi
DataAccessLayer    (AppDbContext + Repositories, EF Core → SQL Server)
```
Chi tiết sơ đồ: xem [`docs/architecture.md`](docs/architecture.md).

**Luồng RAG:** Upload → trích xuất text → chunk → embed → lưu `DocumentChunks`. Hỏi → `RetrievalService` truy xuất top-K chunk (keyword + cosine) → `GroqService` dựng prompt grounded → LLM trả lời + citations.

---

## 3. Yêu cầu môi trường

- **.NET SDK 8** (project target net8.0)
- **SQL Server** (local) — login `admin` / mật khẩu `123`, DB `ChatBotPRN222` (tự tạo khi chạy lần đầu qua EnsureCreated + SQL idempotent)
- **Groq API key** (miễn phí tại https://console.groq.com/keys)
- **Python 3.10+** — cho sidecar embedding (e5/bge-m3/PhoBERT) và bộ đánh giá (eval / RAGAS)

---

## 4. Cấu hình

`PresentationLayer/appsettings.json` (đã .gitignore — tự điền giá trị thật):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChatBotPRN222;User Id=admin;Password=123;TrustServerCertificate=True;"
  },
  "Groq": { "ApiKey": "gsk_...", "Model": "llama-3.3-70b-versatile", "BaseUrl": "https://api.groq.com/openai/v1" },
  "Email": { "SmtpHost": "smtp.gmail.com", "SmtpPort": 587, "FromEmail": "you@gmail.com", "AppPassword": "..." },
  "Embedding": { "AutoStartSidecar": true, "SidecarPort": 8600 }
}
```
Cài đặt RBL (chiến lược chunk, model embedding, key OpenAI/HuggingFace) chỉnh trong app tại **Giám sát hệ thống → Cài đặt RBL** (lưu vào bảng `SystemSettings`).

---

## 5. Chạy dự án

### 5.1. Web app
```bash
cd PresentationLayer
dotnet run
```
→ http://localhost:5300 · HTTPS: https://localhost:7300

App **tự khởi động sidecar embedding** (`tools/embedding_server.py`) khi chạy — lần đầu tự tải model (~vài GB); tắt tính năng bằng `"Embedding:AutoStartSidecar": false`.

> Nếu build báo `file locked by "PresentationLayer (pid)"` → đang có 1 instance chạy, ấn **Ctrl+C** hoặc `taskkill /F /IM PresentationLayer.exe` rồi chạy lại.

### 5.2. Sidecar embedding (cho benchmark e5/bge-m3/PhoBERT)
App tự bật; nếu muốn chạy tay:
```bash
pip install torch --index-url https://download.pytorch.org/whl/cpu
pip install -r tools/requirements.txt
python tools/embedding_server.py        # http://127.0.0.1:8600
```

### 5.3. Tài khoản demo
| Vai trò | Tài khoản | Mật khẩu |
|---|---|---|
| Admin | `admin` | `admin123` |

(Giảng viên/Sinh viên tạo trong **Quản lý người dùng**.)

---

## 6. Đánh giá chất lượng (Deliverables nghiên cứu)

### 6.1. Test set 50 câu + ground truth
`TestSet_50cau_GroundTruth.xlsx` — 50 câu hỏi + câu trả lời đúng do con người soạn (cột: STT · Mã môn · Chủ đề · Câu hỏi · Ground Truth · Tài liệu nguồn).

Chấm **độ chính xác (accuracy)** bằng LLM-as-judge:
```bash
cd eval
pip install -r requirements.txt
python evaluate_chatbot.py --base-url http://localhost:5300   # xuất Eval_Results.xlsx
```

### 6.2. RAGAS benchmark
Đo các chỉ số RAG chuyên biệt (**faithfulness, answer relevancy, context precision, context recall**) — dùng Groq làm judge + embedding local, không cần OpenAI key:
```bash
cd eval
pip install -r ragas_requirements.txt
python ragas_benchmark.py                # xuất RAGAS_Results.xlsx + RAGAS_Results.md
```
Kết quả tóm tắt: xem [`docs/BaoCao_ThucNghiem_RBL.md`](docs/BaoCao_ThucNghiem_RBL.md).

### 6.3. Báo cáo thực nghiệm RBL
Tổng hợp so sánh chunking / embedding / RAG-vs-FT + bảng RAGAS + nhận xét: [`docs/BaoCao_ThucNghiem_RBL.md`](docs/BaoCao_ThucNghiem_RBL.md).

---

## 7. Cấu trúc thư mục
```
PresentationLayer/   # Razor Pages, Controllers API, Hubs, Program.cs, EmbeddingSidecarLauncher
ServiceLayer/        # nghiệp vụ: Chat/Document/Chunking/Retrieval/Groq + RBL comparison + Embeddings
DataAccessLayer/     # EF Core: AppDbContext, Entities, Repositories
tools/               # embedding_server.py (sidecar), requirements.txt, start_embedding_server.bat
eval/                # evaluate_chatbot.py (accuracy), ragas_benchmark.py (RAGAS), test scripts
docs/                # architecture.md/svg, BaoCao_ThucNghiem_RBL.md
TestSet_50cau_GroundTruth.xlsx
```
