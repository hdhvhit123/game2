# Unit test ngoài Unity

Thư mục có dấu `~` nên Unity bỏ qua, không ảnh hưởng project.

- `Tests.cs`, `LevelTests.cs`: 87 bài kiểm thử cho phần logic thuần (mã phòng, lò xo nhân vật, cú nhảy, dây trói,
  dây Verlet, mô hình nước lẩu, vòng đệm khung hình, ghép ảnh highlight, luật màn chơi,
  chấm sao, bệ di động, gió, lưu tiến độ).
- `stubs/`: vỏ API UnityEngine / Photon Fusion 2 / Google Mobile Ads / NativeShare để biên dịch
  toàn bộ script bằng trình biên dịch C# thường (kiểm tra lỗi cú pháp, kiểu dữ liệu).

Chạy (cần .NET SDK 8), từ thư mục gốc repo:

```bash
dotnet new console -o /tmp/T --use-program-main && rm /tmp/T/Program.cs
cp $(find Assets -name "*.cs") Tests~/stubs/*.cs Tests~/*.cs /tmp/T/
cd /tmp/T && dotnet run -p:ImplicitUsings=disable -p:Nullable=disable
```
