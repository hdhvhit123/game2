# Unit test ngoài Unity

Thư mục có dấu `~` nên Unity bỏ qua, không ảnh hưởng project.

- `Tests.cs`: 48 bài kiểm thử cho phần logic thuần (mã phòng, lò xo nhân vật, cú nhảy, dây trói,
  dây Verlet, mô hình nước lẩu, vòng đệm khung hình, ghép ảnh highlight).
- `stubs/`: vỏ API UnityEngine / Photon Fusion 2 / Google Mobile Ads / NativeShare để biên dịch
  toàn bộ script bằng trình biên dịch C# thường (kiểm tra lỗi cú pháp, kiểu dữ liệu).

Chạy (cần .NET SDK 8):

```bash
dotnet new console -o T && rm T/Program.cs
cp Tests.cs stubs/UnityMath.cs stubs/UnityEngineShell.cs T/
cp ../Assets/_Project/Scripts/Core/RoomCode.cs ../Assets/_Project/Scripts/Physics/{RopeSolver,CoopPhysicsMath,PotStability}.cs ../Assets/_Project/Scripts/ViralEngine/FrameRing.cs T/
cd T && dotnet run
```
