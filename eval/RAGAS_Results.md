# Kết quả RAGAS benchmark

- **Model sinh câu trả lời + judge:** Groq `llama-3.3-70b-versatile`
- **Embedding (answer_relevancy):** `multilingual-e5-base` (chạy local)
- **Môn đánh giá:** SWE301 — Kiểm thử phần mềm (môn giao nhau giữa test set và tài liệu đã index)
- **Số câu:** 6 (lấy từ `TestSet_50cau_GroundTruth.xlsx`)

## Trung bình

| Chỉ số RAGAS | Giá trị |
|---|---|
| faithfulness | **0.917** |
| answer_relevancy | **0.953** |
| context_precision | **0.917** |
| context_recall | **1.000** |

## Các câu đã đánh giá (SWE301)

1. Kiểm thử phần mềm là gì / nhằm mục đích gì?
2. Quy trình kiểm thử được chia làm các cấp độ chính nào?
3. Kiểm thử đơn vị (Unit Testing) tập trung vào gì?
4. Ai thường viết Unit Testing?
5. Kiểm thử tích hợp (Integration Testing) kiểm tra điều gì?
6. Kiểm thử hệ thống (System Testing) đánh giá điều gì?

## Ghi chú

- Các chỉ số được tính theo đúng ĐỊNH NGHĨA của RAGAS (faithfulness / answer_relevancy /
  context_precision / context_recall), cài đặt trong `eval/ragas_benchmark.py` dùng Groq làm
  judge + embedding local (không cần OpenAI key, không phụ thuộc thư viện `ragas`).
- Điểm cao (faithfulness/precision ~0.92, recall 1.0, relevancy 0.95) cho thấy câu trả lời
  **bám sát tài liệu**, ngữ cảnh truy xuất **liên quan và đầy đủ**.
- **Tái lập / mở rộng số câu:** cần Groq còn hạn mức token/ngày cho model 70b (judge). Chạy:
  ```bash
  python eval/ragas_benchmark.py --limit 12 --pace 10
  ```
  Nếu key chính hết hạn mức ngày, tạo Groq API key miễn phí mới rồi:
  ```bash
  GROQ_API_KEY=gsk_key_moi python eval/ragas_benchmark.py --limit 12 --pace 10
  ```
  (Model nhỏ `llama-3.1-8b-instant` có hạn mức cao hơn nhưng chấm verdict yes/no kém chính xác,
  chỉ nên dùng để chạy thử pipeline.)
