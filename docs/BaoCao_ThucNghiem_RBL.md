# Báo cáo thực nghiệm — Module nghiên cứu (RBL)
### ChatBot PRN222 · RAG cho tài liệu học tập

---

## 1. Mục tiêu

So sánh & đánh giá các lựa chọn kỹ thuật trong pipeline RAG của chatbot học tập:

1. **Chiến lược chunking** — cắt tài liệu thế nào cho truy xuất tốt.
2. **Model embedding** — mã hoá vector nào cho retrieval chính xác.
3. **RAG vs Fine-tuned (parametric)** — lợi ích của grounding + trích dẫn.
4. **Chất lượng RAG (RAGAS)** — đo faithfulness / relevancy / precision / recall trên test set.

## 2. Thiết lập

| Thành phần | Giá trị |
|---|---|
| LLM sinh câu trả lời | Groq `llama-3.3-70b-versatile` |
| Embedding (thực nghiệm) | `multilingual-e5-base`, `PhoBERT-base`, `bge-m3` (chạy local qua sentence-transformers), `Local (hashing)` (offline), `text-embedding-3-small` (OpenAI — tuỳ chọn) |
| Chunking | Semantic Kernel · Fixed-Size (500 ký tự, overlap 50) · Sentence |
| Truy xuất | keyword (diacritic-insensitive) + cosine khi có vector, top-K=5 |
| Test set | `TestSet_50cau_GroundTruth.xlsx` — 50 câu + ground truth (DBI202, SWE301, OSG202) |
| Môi trường | ASP.NET Core 8, SQL Server, CPU |

> Ghi chú corpus demo: tài liệu đã index còn nhỏ (mỗi môn 1–2 file). Các thí nghiệm chunking/embedding chạy trên môn **PRN222** (Lập trình C# nâng cao); RAGAS chạy trên môn **SWE301** (Kiểm thử phần mềm) — môn giao nhau giữa test set và tài liệu đã index.

---

## 3. Thí nghiệm 1 — So sánh chiến lược Chunking

Câu hỏi: *"LINQ trong C# là gì và dùng để làm gì?"* · môn PRN222 · embedding = `Local`.

| Strategy | #chunk | Độ dài TB | Top score | Avg top-5 | Độ trễ |
|---|---|---|---|---|---|
| Semantic (SK) | 3 | 583 | 0.307 | 0.305 | 949 ms |
| **Fixed Size** | 4 | 449 | **0.319** | **0.319** | **376 ms** |
| Sentence | 10 | 163 | 0.298 | 0.293 | 479 ms |

**Nhận xét:** *Fixed Size* cho retrieval score cao nhất và nhanh nhất trên tài liệu ngắn này; *Sentence* tạo nhiều chunk nhỏ (mật độ cao) nhưng điểm thấp hơn do ngữ cảnh mỗi chunk ít. Với keyword thuần, chênh lệch giữa các strategy nhỏ; khác biệt rõ hơn khi bật embedding.

---

## 4. Thí nghiệm 2 — So sánh Model Embedding

Câu hỏi: *"Async và await trong C# hoạt động thế nào?"* → *"LINQ trong C# là gì?"* · môn PRN222 · 10 chunk (Sentence).

| Model | Provider | Top cosine | Avg top-5 | Embed | Dim |
|---|---|---|---|---|---|
| **multilingual-e5-base** | Local ST | **0.926** | **0.865** | 894 ms | 768 |
| bge-m3 | Local ST | 0.787 | 0.612 | 2601 ms | 1024 |
| PhoBERT-base | Local ST | 0.658 | 0.641 | 894 ms | 768 |
| Local (hashing) | Local | 0.277 | 0.247 | **1 ms** | 256 |
| text-embedding-3-small | OpenAI | — (cần API key) | — | — | 1536 |

**Nhận xét:**
- **multilingual-e5-base** cho retrieval ngữ nghĩa tốt nhất (cosine 0.865) — bắt đúng đoạn về async/await, LINQ dù câu hỏi diễn đạt khác tài liệu.
- **bge-m3** mạnh nhưng nặng (~2.3GB, chậm nhất trên CPU).
- **PhoBERT-base** là MLM tiếng Việt (mean-pooling) → điểm thấp hơn model embedding chuyên dụng, đúng như kỳ vọng.
- **Local (hashing)** là baseline từ vựng: cực nhanh (1 ms) nhưng retrieval yếu nhất — hợp làm mặc định offline, không hợp cho recall ngữ nghĩa.
- `text-embedding-3-small` (OpenAI) chỉ chạy khi cấu hình API key trả phí.

→ **Khuyến nghị:** dùng `multilingual-e5-base` cho retrieval chất lượng (miễn phí, đa ngữ); giữ `Local` làm fallback offline.

---

## 5. Thí nghiệm 3 — RAG vs Fine-tuned (parametric)

Cùng base LLM (`llama-3.3-70b`). Nhánh **RAG** = retrieval + prompt grounded + citation; nhánh **Fine-tuned (mô phỏng)** = trả lời từ kiến thức nội tại, không retrieval.

**Câu trong tài liệu** — *"LINQ trong C# là gì?"*:

| Tiêu chí | RAG | Fine-tuned |
|---|---|---|
| Trả lời | Đúng, dựa tài liệu | Đúng (kiến thức chung) |
| Trích dẫn nguồn | ✅ Có (độ tin cậy 92%) | ❌ Không |
| Bám tài liệu | ✅ Có | ⚠️ Không đảm bảo |

**Câu ngoài tài liệu** — *"Delegate và event trong C# khác nhau thế nào?"* (không có trong doc):

| Tiêu chí | RAG | Fine-tuned |
|---|---|---|
| Trả lời | *"Tôi không tìm thấy… trong tài liệu môn học."* | Trả lời chi tiết từ kiến thức nội tại |
| Nguồn | Không (đúng — không có trong corpus) | Không |
| Rủi ro | Không bịa | **Có thể bịa/không kiểm chứng** |
| Chi phí ước tính | ~7₫ | ~10₫ |

**Nhận xét:** RAG **giảm hallucination** và **chứng minh nguồn**; khi tài liệu không chứa thông tin, RAG **từ chối** thay vì bịa — đúng yêu cầu "giới hạn trong phạm vi tài liệu". Fine-tuned trả lời rộng hơn nhưng không kiểm chứng được và dễ sai lệch ngoài phạm vi môn học.

---

## 6. Thí nghiệm 4 — RAGAS benchmark (chất lượng RAG)

Đo 4 chỉ số chuẩn RAGAS trên **môn SWE301** (Kiểm thử phần mềm) — judge bằng Groq `llama-3.3-70b`, embedding `multilingual-e5-base` (local), **6 câu** từ test set. Chi tiết: `eval/RAGAS_Results.xlsx` / `eval/RAGAS_Results.md`.

| Chỉ số RAGAS | Giá trị trung bình |
|---|---|
| **faithfulness** | **0.917** |
| **answer_relevancy** | **0.953** |
| **context_precision** | **0.917** |
| **context_recall** | **1.000** |

> Cả 4 chỉ số đều cao: câu trả lời **bám tài liệu** (faithfulness 0.917), **sát câu hỏi** (relevancy 0.953), ngữ cảnh truy xuất **liên quan** (precision 0.917) và **đủ** để suy ra đáp án đúng (recall 1.0). Judge dùng model 70b (chính xác); model nhỏ 8b chấm verdict yes/no kém tin cậy nên không dùng cho số liệu chính thức.

**Ý nghĩa các chỉ số:**
- **faithfulness** — câu trả lời có bám ngữ cảnh (không bịa) không.
- **answer_relevancy** — câu trả lời có sát câu hỏi không.
- **context_precision** — đoạn truy xuất có liên quan & xếp hạng tốt không.
- **context_recall** — ngữ cảnh truy xuất có đủ để suy ra ground-truth không.

---

## 7. Kết luận

1. **Chunking:** *Fixed-Size* cân bằng tốt tốc độ/độ chính xác trên tài liệu ngắn; nên kiểm thử lại khi corpus lớn hơn.
2. **Embedding:** *multilingual-e5-base* là lựa chọn retrieval tốt nhất trong nhóm miễn phí; *Local (hashing)* làm fallback offline.
3. **RAG vs Fine-tuned:** RAG vượt trội về tính kiểm chứng (citation) và an toàn (từ chối khi ngoài phạm vi) — phù hợp bài toán trợ giảng theo tài liệu.
4. **RAGAS:** hệ thống đạt điểm cao trên môn có tài liệu đầy đủ (xem bảng), khẳng định câu trả lời **grounded** và **liên quan**. Điểm sẽ phản ánh sát hơn khi mở rộng corpus tài liệu cho tất cả các môn trong test set.

**Hạn chế & hướng phát triển:** corpus demo còn nhỏ (nhiều môn trong test set chưa có tài liệu → chatbot từ chối là đúng nhưng làm giảm phạm vi đánh giá). Mở rộng: upload đầy đủ tài liệu các môn, bật embedding e5 cho index chat, tăng cỡ test set.
