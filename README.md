# ChatBotPRN222 — Hệ Thống Hỏi Đáp Tài Liệu Học Tập (RAG) 🚀

Dự án **ChatBotPRN222** là ứng dụng Web **ASP.NET Core MVC (.NET 8.0)** dùng **SQL Server + Entity Framework Core**, kết hợp mô hình ngôn ngữ lớn **Groq (Llama 3.3)** để: trích xuất nội dung tài liệu bài giảng (**PDF, DOCX, PPTX, TXT**), chia thành các đoạn (**chunk**), và cho phép sinh viên đặt câu hỏi trực tiếp dựa trên ngữ cảnh tài liệu — mô hình **RAG (Retrieval-Augmented Generation)** kèm **trích dẫn nguồn (citation)**.

> **Chunking:** tài liệu được cắt bằng **Microsoft Semantic Kernel `TextChunker`** (đo theo ký tự: dòng ≤200, đoạn ≤800 ký tự, overlap 100). Bạn có thể xem chi tiết cách tài liệu được chunk ngay trong trang xem tài liệu.

---

## ⚡ 1. Tính Năng Chính

- 🔐 **Xác thực & 3 vai trò:** Admin, Lecturer (Giảng viên), Student (Sinh viên) — đăng nhập bằng Cookie Authentication.
- 📧 **Đăng ký có xác thực OTP qua email** (gửi mã 6 số, hết hạn 5 phút).
- 📋 **Whitelist email đăng ký** *(mới)* — Admin quản lý danh sách email được phép tạo tài khoản.
- 📚 **Quản lý môn học (Subject) → chương (Chapter) → tài liệu (Document)** *(Chapter là tính năng mới)*.
- 📤 **Tải lên tài liệu** PDF / DOCX / PPTX / TXT (tối đa 2 GB), tự động **chống trùng theo nội dung (SHA-256)**.
- ✂️ **Chia nhỏ thành chunks** + xem lại cách chunk; **chunk lại (re-chunk)** khi cần.
- 📊 **Theo dõi trạng thái tài liệu** *(mới — pipeline)*: `Processing → Indexed` (hoặc `Empty` / `Failed`).
- 🤖 **Chatbot RAG** trả lời theo tài liệu đã index, kèm **trích dẫn nguồn** (tài liệu, trang, đoạn).
- 💬 **Lịch sử hội thoại theo phiên (session)** cho từng người dùng.
- 📨 **Góp ý (Feedback) + trả lời** giữa người dùng và Admin.
- 📈 **Dashboard** thống kê cho Admin.
- 👤 **Hồ sơ cá nhân** (avatar, bio).
- 🧪 **Bộ test set 50 câu + script chấm điểm tự động** (LLM-as-judge).

---

## 📌 2. Yêu Cầu Chuẩn Bị (Prerequisites)

1. **.NET 8.0 SDK** (bắt buộc) — tải tại [dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).
2. **Visual Studio 2022** (khuyên dùng trên Windows) hoặc **Visual Studio Code** (cài thêm **C# Dev Kit**).
3. **SQL Server** (LocalDB, Express hoặc bản đầy đủ) — lần chạy đầu EF Core tự tạo schema và seed dữ liệu mẫu.
4. **Mạng Internet** — để gọi **Groq AI API** (đã cấu hình sẵn) và gửi **email OTP**.

---

## 🛠️ 3. Cấu Hình (Configuration)

Toàn bộ cấu hình nằm trong **`ChatBotPRN222/appsettings.json`**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ChatBotPRN222;User Id=YOUR_USER;Password=YOUR_PASS;TrustServerCertificate=True;"
  },
  "Groq": {
    "ApiKey": "gsk_...",
    "Model": "llama-3.3-70b-versatile",
    "BaseUrl": "https://api.groq.com/openai/v1"
  },
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "FromEmail": "your_email@gmail.com",
    "FromName": "ChatBot PRN222",
    "AppPassword": "your_gmail_app_password"
  }
}
```

| Tham số | Mô tả |
|---|---|
| `ConnectionStrings:DefaultConnection` | Trỏ tới SQL Server của bạn. DB và bảng được EF Core tạo tự động (`EnsureCreated` + script vá cột/bảng) ở lần chạy đầu. |
| `Groq:ApiKey` / `Model` | API key và model LLM (mặc định `llama-3.3-70b-versatile`). |
| `Email:*` | Cấu hình SMTP để gửi OTP (mặc định Gmail + App Password). |

> ⚠️ Không commit `appsettings.json` chứa mật khẩu/khoá thật lên GitHub công khai.

---

## 🏃‍♂️ 4. Cách Chạy (How to Run)

### Cách 1 — Dòng lệnh (nhanh nhất)
```bash
cd ChatBotPRN222          # thư mục chứa Program.cs
dotnet run
```
Mở trình duyệt: **http://localhost:5216** hoặc **https://localhost:7216** (hoặc cổng hiển thị trên Terminal).

### Cách 2 — Visual Studio 2022
1. Mở **`ChatBotPRN222.sln`**.
2. Đợi load các project con, nhấn **F5** (có debug) hoặc **Ctrl+F5** (không debug).

### (Tuỳ chọn) Tạo schema bằng SQL script thủ công
File **`ChatBotPRN222DB.sql`** chứa script tạo đầy đủ các bảng (idempotent, có thể chạy lại). Mở trong SSMS và Execute nếu muốn tạo DB trước khi chạy app.

---

## 👥 5. Vai Trò & Phân Quyền (Roles & Authorization)

| Vai trò | Quyền chính |
|---|---|
| **Admin** | Quản lý người dùng, cấp/thu hồi quyền upload cho giảng viên, quản lý môn học, **whitelist email**, quản lý góp ý, xem Dashboard. Admin luôn được upload mọi môn. |
| **Lecturer** | Upload & quản lý tài liệu **cho đúng bộ môn được Admin cấp quyền**, tạo/sửa/xoá **chương** của bộ môn đó. |
| **Student** | Chat hỏi đáp, xem tài liệu & chương, xem trích dẫn, gửi góp ý, xem lịch sử hội thoại. |

**Authorization policies** (khai báo trong `Program.cs`):

| Policy | Ai được phép | Dùng cho |
|---|---|---|
| `AdminOnly` | Admin | Quản lý người dùng, whitelist email, quản lý góp ý |
| `LecturerOrAdmin` | Lecturer, Admin | Khu vực quản trị nội dung |
| `CanUploadDocuments` | Admin (luôn) hoặc Lecturer **đã được cấp quyền** | Upload/xoá/chunk lại tài liệu, tạo/sửa/xoá chương |

---

## 🗂️ 6. Cấu Trúc Nội Dung: Subject → Chapter → Document

Tài liệu được tổ chức 3 cấp:

```
Môn học (Subject)
└── Chương (Chapter)          ← MỚI: nhóm tài liệu theo chương, có thứ tự
    └── Tài liệu (Document)   ← gán chương khi upload (tuỳ chọn)
```

- Vào menu **Chương** để tạo/sửa/xoá chương cho từng môn (Admin hoặc giảng viên được cấp quyền).
- Khi **upload tài liệu**, có thể chọn chương để gán (dropdown chương tự lọc theo môn đang chọn). Bỏ trống = tài liệu không thuộc chương nào.
- Danh sách tài liệu có **bộ lọc theo chương** và cột hiển thị **chương + trạng thái**.
- Xoá một chương **không xoá** tài liệu — tài liệu chỉ bị **gỡ liên kết** (ChapterId = null).

---

## 🔄 7. Quy Trình Upload & Xử Lý Tài Liệu (Status Pipeline)

```
1. Chọn môn (+ chương tuỳ chọn) và tệp
        ↓
2. Validate loại file (.pdf/.docx/.pptx/.txt) & kích thước (≤ 2GB)
        ↓
3. Hash SHA-256 → bỏ qua nếu trùng hệt; cùng tên khác nội dung → thay bản mới
        ↓
4. Tạo record Document  →  Status = Processing
        ↓
5. Trích xuất text  (PDF: PdfPig · DOCX/PPTX: OpenXml · DOCX xem đẹp: Mammoth)
        ↓
6. Chia thành chunks (Semantic Kernel TextChunker)
        ↓
7. Lưu chunks vào DB
        ↓
8. Cập nhật Status:  Indexed ✅ (có chunk) · Empty (0 chunk) · Failed ❌ (lỗi)
```

**Các trạng thái** hiển thị bằng badge màu trong danh sách & trang xem tài liệu:

| Status | Ý nghĩa |
|---|---|
| `Processing` | Đang trích xuất + chunk |
| `Indexed` | Đã chunk xong, sẵn sàng cho chatbot |
| `Empty` | Xử lý xong nhưng không tách được chunk nào |
| `Failed` | Trích xuất/chunk lỗi (record vẫn hiển thị để biết và thử lại) |

---

## 📧 8. Whitelist Email Đăng Ký

- Vào menu **Email cho phép** (Admin) để quản lý danh sách email được phép đăng ký.
- **Danh sách trống → đăng ký mở** cho mọi email (giữ tương thích hành vi cũ).
- **Có ≥ 1 email → whitelist BẬT:** chỉ những email trong danh sách mới đăng ký được; người dùng nhập email ngoài danh sách bị chặn ngay (trước cả bước gửi OTP).

---

## 🏗️ 9. Kiến Trúc Hệ Thống (Architecture)

Giải pháp chia **3 tầng** (3-layer), tham chiếu một chiều `Web → ServiceLayer → DataAccessLayer`:

```
ChatBotPRN222 (Web · MVC)
   Controllers · Views · ViewModels · Seeders
        │  gọi
        ▼
ServiceLayer (nghiệp vụ)
   AuthService · ChatService · DocumentService · ChapterService
   AllowedEmailService · SubjectService · FeedbackService
   DashboardService · GroqService · EmailService
   TextExtractor · TextChunkerChunker
        │  gọi
        ▼
DataAccessLayer (dữ liệu)
   AppDbContext (EF Core) · Entities · Repositories · Constants
```

| Thành phần | Công nghệ |
|---|---|
| **Framework** | .NET 8, ASP.NET Core MVC |
| **Database** | SQL Server + Entity Framework Core 8 |
| **Authentication** | Cookie Authentication + Authorization Policies |
| **LLM** | Groq API (Llama 3.3 70B) |
| **Chunking** | Microsoft Semantic Kernel `TextChunker` |
| **PDF** | UglyToad.PdfPig |
| **Office (DOCX/PPTX)** | DocumentFormat.OpenXml |
| **DOCX → HTML xem đẹp** | Mammoth |
| **Mật khẩu** | BCrypt.Net |
| **Email OTP** | SMTP (Gmail App Password) |

> Sơ đồ kiến trúc chi tiết: xem `docs/architecture.svg` và `docs/architecture.md`.

---

## 🔑 10. Tài Khoản Thử Nghiệm (Seeded Test Accounts)

Lần chạy đầu, hệ thống tự seed: **4 môn** (`PRN222`, `DBI202`, `SWE301`, `OSG202`), **4 tài liệu mẫu** (trong `ChatBotPRN222/SeedData/`, tự index vào đúng môn), và **3 tài khoản**:

| Vai trò | Username | Password | Chức năng chính |
|---|---|---|---|
| **Admin** | `admin` | `admin123` | Quản lý người dùng, whitelist, môn học, góp ý, dashboard |
| **Lecturer** | `lecturer` | `lecturer123` | Upload tài liệu, quản lý chương (sau khi được Admin cấp quyền bộ môn) |
| **Student** | `student` | `student123` | Chat hỏi đáp dựa trên tài liệu đã index |

💡 **Mẹo:** đăng nhập `lecturer` để upload tài liệu & tạo chương; đăng nhập `student` để chat hỏi đáp.

---

## 🧪 11. Đánh Giá Chatbot Bằng Test Set (50 câu + Ground Truth)

Đi kèm dự án:
- **`TestSet_50cau_GroundTruth.xlsx`** — 50 câu hỏi + đáp án đúng (ground truth), trải trên 3 môn `DBI202`, `SWE301`, `OSG202`.
- **`eval/evaluate_chatbot.py`** — script chạy 50 câu qua chatbot, so với ground truth và tính **Accuracy**.

### Cách chấm điểm tự động
1. **Chạy web app** trước (mục 4) và ghi nhớ địa chỉ, ví dụ `http://localhost:5216`.
2. Cài thư viện Python và chạy:
   ```bash
   cd eval
   pip install -r requirements.txt
   python evaluate_chatbot.py --base-url http://localhost:5216
   ```
3. Script sẽ: đăng nhập `student`, tạo phiên chat (toàn bộ tài liệu), gửi lần lượt 50 câu, dùng **Groq (LLM-as-judge)** chấm Đúng/Sai (không có key/mạng sẽ tự chuyển sang chấm bằng độ trùng từ khoá), rồi in **Accuracy** và xuất chi tiết ra **`eval/Eval_Results.xlsx`**.

Tham số hữu ích: `--username/--password`, `--no-judge`, `--limit N`, `--groq-key`.

---

## 🛠️ 12. Khắc Phục Sự Cố (Troubleshooting)

**❌ "Cannot connect to database"**
- Kiểm tra SQL Server đang chạy và `ConnectionStrings:DefaultConnection` đúng (server, user, password).
- App vẫn khởi động được nếu DB lỗi (chỉ log cảnh báo), nhưng các chức năng dữ liệu sẽ không hoạt động.

**❌ Không gửi được OTP khi đăng ký**
- Kiểm tra mục `Email` trong `appsettings.json`: `FromEmail` + `AppPassword` (Gmail cần bật **App Password**, không dùng mật khẩu thường).

**❌ "Unauthorized" / không thấy nút Upload**
- Tài khoản giảng viên cần được **Admin cấp quyền upload cho một bộ môn** (menu Người dùng).

**❌ Không đăng ký được tài khoản mới**
- Có thể **whitelist email đang BẬT**. Vào menu *Email cho phép* (Admin) để thêm email hoặc xoá hết để mở đăng ký.

**❌ Upload thất bại / tài liệu báo `Failed`**
- File có thể hỏng hoặc không trích xuất được text. Thử **Chunk lại** trong trang xem tài liệu, hoặc upload bản khác.

---

## 📦 13. Cấu Trúc Thư Mục (rút gọn)

```
ChatBotPRN222.sln
├── ChatBotPRN222/            # Web (MVC): Controllers, Views, Models, Program.cs, SeedData/, eval không nằm ở đây
├── ServiceLayer/             # Nghiệp vụ: Services, Dtos, Settings
├── DataAccessLayer/          # EF Core: Context, Entities, Repositories, Constants
├── eval/                     # Script chấm điểm (Python)
├── docs/                     # Sơ đồ kiến trúc
├── ChatBotPRN222DB.sql       # Script tạo schema thủ công (tuỳ chọn)
└── TestSet_50cau_GroundTruth.xlsx
```
