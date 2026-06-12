# Chatbot tư vấn dựa trên tài liệu với ASP.NET Core Web API và React

## Giới thiệu dự án

Đây là hệ thống chatbot tư vấn dựa trên tài liệu được upload. Người dùng có thể tải tài liệu PDF lên hệ thống, backend sẽ xử lý nội dung tài liệu, chia nhỏ thành các đoạn, tạo embedding và lưu vào cơ sở dữ liệu. Khi người dùng đặt câu hỏi, hệ thống sẽ tìm các đoạn tài liệu liên quan nhất và sử dụng AI để tạo câu trả lời phù hợp.

Dự án được xây dựng nhằm thực hành phát triển Web Application với backend ASP.NET Core Web API, frontend React + Vite, cơ sở dữ liệu PostgreSQL và tích hợp Gemini API cho chức năng chatbot.

---

## Chức năng chính

* Upload tài liệu PDF vào hệ thống.
* Đọc và xử lý nội dung tài liệu.
* Chia tài liệu thành các đoạn nhỏ để phục vụ tìm kiếm.
* Tạo embedding cho nội dung tài liệu bằng Gemini Embedding API.
* Lưu thông tin tài liệu, nội dung chunk và vector vào PostgreSQL/pgvector.
* Cho phép người dùng đặt câu hỏi trên giao diện chatbot.
* Tìm kiếm các đoạn tài liệu liên quan đến câu hỏi của người dùng.
* Gọi Gemini Chat API để sinh câu trả lời dựa trên dữ liệu tìm được.
* Lưu lịch sử hội thoại.
* Hỗ trợ xác thực/phân quyền bằng JWT.
* Kiểm thử API thông qua Swagger/Postman.
* Hỗ trợ chạy môi trường bằng Docker/docker-compose.

---

## Công nghệ sử dụng

### Backend

* C#
* ASP.NET Core Web API
* Entity Framework Core
* PostgreSQL
* pgvector
* JWT Authentication/Authorization
* Gemini API
* Swagger
* Docker

### Frontend

* React
* Vite
* JavaScript
* CSS

### Công cụ

* Visual Studio
* Visual Studio Code
* Git/GitHub
* Postman/Swagger
* Docker Desktop

---

## Kiến trúc dự án

Backend được chia theo nhiều tầng để dễ bảo trì và mở rộng:

```text
Chat_API/
│
├── Chat_API/                  # API layer: Controllers, Middleware, Program.cs
├── Chatbot_Application/       # Application layer: Services, DTOs, Interfaces
├── Chatbot_Domain/            # Domain layer: Entities, Models
├── Chatbot_Infrastructure/    # Infrastructure layer: Database, Repositories, External services
└── Chat_API.Tests/            # Unit tests
```

Frontend:

```text
Chat_Frontend/
│
├── public/
├── src/
├── package.json
└── vite.config.js
```

---

## Luồng hoạt động của hệ thống

### 1. Luồng upload và xử lý tài liệu

```text
User/Admin upload PDF
→ Frontend gửi file lên backend API
→ Backend kiểm tra file
→ Lưu file vào thư mục uploads
→ Tạo bản ghi Document trong database
→ Đọc nội dung PDF
→ Chia nội dung thành các chunk nhỏ
→ Gọi Gemini Embedding API để tạo vector cho từng chunk
→ Lưu chunk và vector vào PostgreSQL/pgvector
```

### 2. Luồng hỏi đáp chatbot

```text
User nhập câu hỏi
→ Frontend gửi câu hỏi lên API
→ Backend tạo embedding cho câu hỏi
→ So sánh vector câu hỏi với vector tài liệu
→ Lấy các chunk liên quan nhất
→ Kiểm tra độ tương đồng
→ Nếu không có dữ liệu phù hợp thì trả fallback
→ Nếu có dữ liệu phù hợp thì tạo prompt
→ Gọi Gemini Chat API
→ Nhận câu trả lời
→ Lưu lịch sử hội thoại
→ Trả kết quả về frontend
```

---

## Mô hình RAG trong dự án

Dự án áp dụng hướng tiếp cận RAG, viết tắt của Retrieval-Augmented Generation.

Thay vì để AI trả lời tự do, hệ thống sẽ:

1. Truy xuất các đoạn tài liệu liên quan đến câu hỏi.
2. Đưa các đoạn này vào prompt làm ngữ cảnh.
3. Yêu cầu AI chỉ trả lời dựa trên thông tin trong tài liệu.
4. Nếu không có dữ liệu phù hợp, chatbot sẽ trả lời fallback thay vì tự bịa thông tin.

Điều này giúp câu trả lời chính xác hơn và bám sát tài liệu đã được upload.

---

## Database chính

Một số bảng/entity chính trong hệ thống:

* `Document`: lưu thông tin tài liệu PDF được upload.
* `DocumentChunk`: lưu các đoạn nhỏ được tách ra từ tài liệu, kèm embedding vector.
* `Conversation`: lưu thông tin cuộc hội thoại.
* `ConversationMessage`: lưu từng tin nhắn của người dùng và chatbot.

Quan hệ cơ bản:

```text
1 Document có nhiều DocumentChunk
1 Conversation có nhiều ConversationMessage
```

---

## API chính

Một số nhóm API chính trong dự án:

### Chat API

* Gửi câu hỏi từ người dùng.
* Tìm kiếm tài liệu liên quan.
* Gọi AI để tạo câu trả lời.
* Lưu lịch sử hội thoại.

Ví dụ endpoint:

```text
POST /api/chat/ask
```

### Document API

* Upload tài liệu PDF.
* Xem danh sách tài liệu.
* Xem trạng thái xử lý tài liệu.
* Xóa tài liệu.

Ví dụ endpoint:

```text
POST /api/documents/upload
GET /api/documents
DELETE /api/documents/{id}
```

---

## Cài đặt và chạy dự án

### Yêu cầu môi trường

Cần cài đặt:

* .NET SDK
* Node.js
* Docker Desktop
* Git

---

## Chạy backend bằng Docker

Di chuyển vào thư mục backend:

```bash
cd Chat_API
```

Tạo file `.env` từ file mẫu nếu có:

```bash
cp .env.example .env
```

Cấu hình các biến môi trường cần thiết trong `.env`, ví dụ:

```env
GEMINI_API_KEY=your_gemini_api_key
JWT_KEY=your_jwt_secret_key
POSTGRES_USER=postgres
POSTGRES_PASSWORD=your_password
POSTGRES_DB=chatbot_db
```

Chạy backend và database:

```bash
docker compose up -d --build
```

Sau khi chạy thành công, backend có thể truy cập tại:

```text
http://localhost:8080
```

Swagger:

```text
http://localhost:8080/swagger
```

---

## Chạy backend local bằng .NET

Di chuyển vào thư mục backend API:

```bash
cd Chat_API/Chat_API
```

Khôi phục package:

```bash
dotnet restore
```

Chạy migration nếu cần:

```bash
dotnet ef database update
```

Chạy backend:

```bash
dotnet run
```

---

## Chạy frontend

Di chuyển vào thư mục frontend:

```bash
cd Chat_Frontend
```

Cài đặt package:

```bash
npm install
```

Chạy frontend:

```bash
npm run dev
```

Frontend thường chạy tại:

```text
http://localhost:5173
```

---

## Cấu hình bảo mật

Dự án sử dụng các thông tin nhạy cảm như:

* Gemini API Key
* JWT Secret Key
* Database Connection String
* Database Password

Không nên lưu trực tiếp các thông tin này trong source code. Nên sử dụng:

* `.env`
* User Secrets của .NET
* Environment Variables
* Secret Manager khi triển khai production

File `.env` thật nên được đưa vào `.gitignore`.

---

## Một số điểm đã thực hiện

* Xây dựng backend bằng ASP.NET Core Web API.
* Tổ chức backend theo nhiều tầng: API, Application, Domain, Infrastructure.
* Kết nối PostgreSQL bằng Entity Framework Core.
* Sử dụng pgvector để lưu và xử lý embedding vector.
* Tích hợp Gemini API cho embedding và chatbot response.
* Xây dựng API upload tài liệu PDF.
* Xử lý tài liệu, chia chunk và lưu dữ liệu vào database.
* Xây dựng luồng hỏi đáp dựa trên mô hình RAG.
* Cấu hình JWT Authentication/Authorization.
* Kiểm thử API bằng Swagger/Postman.
* Cấu hình Docker/docker-compose cho backend và database.
* Xây dựng frontend bằng React + Vite.

---

## Hướng phát triển tiếp theo

* Hoàn thiện giao diện người dùng.
* Bổ sung phân quyền rõ hơn cho từng nhóm người dùng.
* Cải thiện xử lý upload file an toàn hơn.
* Bổ sung kiểm tra MIME type và giới hạn dung lượng file chi tiết hơn.
* Bổ sung unit test và integration test.
* Tối ưu truy vấn vector search.
* Bổ sung cache cho các dữ liệu thường dùng.
* Triển khai hệ thống lên cloud/server.
* Bổ sung logging, monitoring và error tracking.
* Cải thiện README với hình ảnh demo và hướng dẫn chi tiết hơn.

---

## Tác giả

**Dương Văn Quang**

GitHub: [github.com/quangdz05](https://github.com/quangdz05)

Repository: [github.com/quangdz05/chatbot](https://github.com/quangdz05/chatbot)
