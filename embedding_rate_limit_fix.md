# Embedding Rate Limit Fix — Tổng kết thay đổi

## Vấn đề gốc
`GeminiEmbeddingService.GenerateBatchEmbeddingAsync()` gọi API **từng chunk một** trong vòng `foreach`, không có xử lý rate limit → lỗi 429 khi có nhiều chunks.

## Tổng quan thay đổi

### 1. Entity: DocumentChunk — Thêm fields mới
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Domain/Entities/DocumentChunk.cs)

| Field | Mục đích |
|-------|----------|
| `ContentHash` (string?) | SHA256 của content → tránh embedding trùng |
| `EmbeddingStatus` (enum) | `Pending` → `Processing` → `Embedded` / `Failed` |
| `ErrorMessage` (string?) | Chi tiết lỗi khi embedding thất bại |
| `ComputeHash()` (static) | Helper tính SHA256 |

### 2. Entity: Document — Thêm fields mới
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Domain/Entities/Document.cs)

| Thay đổi | Mục đích |
|----------|----------|
| Enum `Embedded` | Phân biệt parsed+chunked vs đã embedding |
| `ErrorMessage` | Lưu lỗi cụ thể (ví dụ: "5 of 50 chunks failed") |

### 3. GeminiEmbeddingService — Viết lại hoàn toàn
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Infrastructure/Services/GeminiEmbeddingService.cs)

**Thay đổi chính:**
- ✅ Dùng `batchEmbedContents` API (gom nhiều text trong 1 request) thay vì `embedContent` riêng lẻ
- ✅ Auto-split thành sub-batches 25 chunks
- ✅ **Exponential backoff** khi gặp 429: `2s × 2^attempt ± 20% jitter`, retry tối đa 5 lần
- ✅ Delay 2s giữa mỗi batch
- ✅ Fallback sang `gemini-embedding-001` nếu model 404

### 4. DocumentProcessingService — Refactor logic embedding
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Application/Services/DocumentProcessingService.cs)

**Flow mới:**
1. Parse PDF → Chunk → Tính `ContentHash` cho mỗi chunk
2. Query DB: chunk nào đã có hash + embedding → **skip** (không gọi API lại)
3. Lưu tất cả chunks vào DB với status `Pending`
4. Embed theo batch 25 chunks, delay 2s giữa batches
5. Lỗi 1 batch **không crash** cả document — đánh dấu `Failed` riêng batch đó
6. Cập nhật document status: `Completed` (full/partial) hoặc `Failed`

### 5. IDocumentChunkRepository — Thêm methods mới
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Application/Interfaces/Repositories/IDocumentChunkRepository.cs)

| Method | Mục đích |
|--------|----------|
| `UpdateRangeAsync()` | Cập nhật embedding + status hàng loạt |
| `GetExistingEmbeddedHashesAsync()` | Tra cứu hash đã có embedding → skip |
| `GetPendingChunksByDocumentIdAsync()` | Lấy chunks chưa embed (cho retry) |

### 6. DocumentChunkRepository — Implement methods mới
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Infrastructure/Repositories/DocumentChunkRepository.cs)

### 7. AppDbContext — Cấu hình EF Core cho fields mới
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Infrastructure/Data/AppDbContext.cs)

- Index trên `ContentHash` để tra cứu nhanh
- `EmbeddingStatus` convert sang string
- `ErrorMessage` max 2000 chars

### 8. DbSeeder — Thêm delay giữa files
render_diffs(file:///d:/Dự%20án%20SSI/Chat_API/Chat_API/Chatbot_Infrastructure/Data/DbSeeder.cs)

- Thêm `await Task.Delay(3000)` giữa mỗi file khi seeding

## Không thay đổi
- ❌ Tên class, route API, database schema cũ
- ❌ Frontend
- ❌ Kiến trúc project (vẫn Clean Architecture)
- ❌ Controller logic (DocumentsController vẫn dùng queue)
- ❌ Chat flow (không ảnh hưởng)

## Migration
Migration EF Core đã được tạo tự động:
```
AddChunkEmbeddingStatusAndContentHash
```

> [!IMPORTANT]
> Chạy `dotnet ef database update` hoặc để `DbSeeder.SeedAsync()` tự migrate khi khởi động app.

## Rate Limit Strategy tổng kết

```
┌─ Upload API ─────────────────────────────┐
│  Lưu file → DB (Queued) → Queue job      │
│  → Trả response ngay (202 Accepted)      │
└──────────────────────────────────────────┘
         ↓ Background Worker
┌─ DocumentProcessingService ──────────────┐
│  1. Parse PDF → Chunk                     │
│  2. Tính ContentHash → Skip trùng         │
│  3. Lưu chunks (Pending)                  │
│  4. Embed batch 25 chunks                 │
│     ├─ Delay 2s giữa batches             │
│     └─ GeminiEmbeddingService             │
│         ├─ batchEmbedContents API         │
│         ├─ Retry 429 (5 lần)             │
│         └─ Backoff: 2s × 2^n ± jitter   │
│  5. Update status từng chunk/document     │
└──────────────────────────────────────────┘
```
