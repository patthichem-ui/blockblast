# Block Blast — C# WinForms

Game xếp khối trên bàn **8 × 8**, viết bằng **C# / .NET 9 / Windows Forms**. Đây là bộ luật và thuật toán tự triển khai cho một game kiểu Block Blast; có thể đọc, sửa và dùng làm nền cho dự án của bạn.

## Chạy trên Windows

Yêu cầu Windows 10/11 và **.NET 9 SDK 9.0.305 hoặc mới hơn trong dòng .NET 9**. SDK **9.0.306** đã được dùng để kiểm tra bản này. `global.json` cho phép dùng phiên bản SDK 9 tương thích đã cài trên máy; không cần cài .NET 10. Mở PowerShell tại thư mục chứa README này:

```powershell
dotnet restore BlockBlast.sln
dotnet build BlockBlast.sln -c Release --no-restore
dotnet run --project src/BlockBlast.WinForms -c Release --no-build
```

Hoặc bấm đúp **`Chay-Game.cmd`** sau khi giải nén source để tự build và mở game bằng SDK .NET 9 đang cài. Lần chạy đầu cần Internet để tải reference pack khi chưa có trong cache.

Hoặc mở `BlockBlast.sln` bằng Visual Studio có hỗ trợ .NET 9, chọn `BlockBlast.WinForms` làm startup project rồi nhấn F5. Không cần database, khóa API hay thư viện NuGet bên thứ ba.

Nếu đã tải bản .NET 10 trước đó, hãy tải lại source trên nhánh `codex/blockblast-winforms` và giải nén vào **thư mục mới**. Nếu dùng Git, cập nhật bằng `git pull` rồi chạy `dotnet restore BlockBlast.sln` trước khi build. Bản cũ có thể giữ `global.json` hoặc output `bin/obj` của .NET 10.

WinForms chạy trên Windows. Engine và bộ kiểm thử chạy được trên Windows, Linux và macOS.

## Cách chơi

- Kéo một trong ba khối ở khay xuống bàn; hình xem trước cho biết vị trí có hợp lệ không.
- Có thể bấm chọn khối rồi bấm ô trên bàn. Điểm neo là ô góc trên bên trái của hình chữ nhật bao khối.
- Phím `1`, `2`, `3` chọn khối; phím mũi tên và `Enter` dùng để đặt bằng bàn phím; `Esc` hủy chọn; `R` chơi lại.
- Khối không được chồng lên ô đã có hoặc vượt ra ngoài bàn. Khối đã sinh giữ nguyên hướng, không xoay trong lượt chơi.
- Hàng và cột đầy được xóa **đồng thời**. Ô giao nhau chỉ được xóa một lần nhưng cả hàng và cột đều được tính điểm.
- Dùng hết cả ba khối mới nhận khay tiếp theo. Game kết thúc khi **mọi khối còn lại** đều không có vị trí đặt hợp lệ.
- Điểm cao nhất được lưu trong thư mục dữ liệu ứng dụng của tài khoản Windows, không ghi vào source code.

## Cấu trúc source

```text
src/BlockBlast.Core/          Bàn chơi, hình khối, sinh khay, điểm và độ khó
src/BlockBlast.WinForms/      Giao diện, tương tác và lưu điểm cao
tests/BlockBlast.Core.Tests/  Bộ kiểm thử console, không cần thư viện test ngoài
```

Engine tách khỏi giao diện để bạn có thể dùng lại trong một giao diện C# khác.

## Logic tính điểm

Gọi `N` là số ô của khối vừa đặt, `L` là tổng hàng + cột được xóa, `D` là cấp độ **trước lượt đặt**, `C` là combo sau lượt đặt:

```text
Điểm đặt khối = 5 × N
Điểm xóa      = 100 × L² × D
Điểm combo    = 50 × (C − 1) × L × D     (chỉ khi L > 0)
Điểm lượt     = Điểm đặt khối + Điểm xóa + Điểm combo
```

Combo tăng 1 khi một lượt xóa được ít nhất một hàng/cột; đặt mà không xóa thì combo về 0. Một lượt xóa nhiều hàng vẫn tăng combo một lần. Ví dụ, khối 3 ô xóa 2 hàng/cột ở cấp 1, combo 2: `15 + 400 + 100 = 515 điểm`. Lượt sai không cộng điểm, không mất khối và không thay đổi combo.

`ScoreCalculator` chứa công thức; `GameEngine.TryPlace` áp dụng điểm và cập nhật trạng thái. Tổng điểm dùng `long`.

## Logic tăng độ khó

```text
Level = min(20, 1 + max(
    floor(tổng hàng/cột đã xóa / 10),
    floor(số khối đã đặt / 45),
    floor(sqrt(tổng điểm / 2500))
))
```

`DifficultyProfile.ForProgress` tập trung các tham số điều chỉnh. Level không giảm khi bàn chật. Khi level tăng, kích thước mục tiêu và độ phân tán của khối tăng; những hình kém gọn được ưu tiên hơn. Điểm thưởng xóa hàng/cột cũng nhân với level. Độ khó là cách chọn khối và phân bố xác suất, không phải giới hạn thời gian.

## Thuật toán sinh khay 3 khối

Các bước chính nằm trong `PieceGenerator.Generate`:

1. `ShapeCatalog` tạo **40 biến thể** từ ô đơn, thanh, vuông, L/J/T/S/Z, dấu cộng và chữ nhật. Mỗi hình được xoay 90°, chuẩn hóa tọa độ và loại bỏ hướng xoay trùng.
2. Tính trọng số theo level, số ô, độ dài, khoảng trống trong hình và độ kín của bàn. Bàn chật làm kích thước mục tiêu nhỏ lại. Hình đã chọn trong khay được nhân trọng số `0.25`, vẫn cho phép trùng.
3. Chọn khối đầu từ tập hình **có vị trí đặt ngay**, hai khối sau từ toàn bộ catalog, rồi xáo khay bằng Fisher–Yates.
4. `TraySolver` tìm một thứ tự đặt được **cả ba khối**, thử các vị trí và mô phỏng xóa hàng/cột sau từng bước. Khối chưa đặt được lúc đầu vẫn có thể đặt được sau khi khối khác xóa hàng.
5. Nếu chưa tìm được nghiệm, thử khay khác. Giới hạn mặc định: tối đa 12 khay ở level thấp, 10 khay từ level 10; 2.048 trạng thái mỗi lần tìm và 16.384 trạng thái cho toàn bộ lần sinh.
6. Khi hết ngân sách, dựng khay dự phòng: đặt lần lượt ba hình nhỏ có vị trí hợp lệ trên một bản sao bàn, lưu thứ tự/vị trí làm chứng cứ, rồi xáo khay và cập nhật chỉ số. Ô đơn bảo đảm có lựa chọn trên trạng thái bàn hợp lệ, vì hàng/cột đầy luôn đã được xóa.

Khay mới luôn có ít nhất **một cách dùng hết ba khối**. Người chơi vẫn có thể chọn một vị trí khác làm khối còn lại không đặt được. Không sinh lại khay giữa chừng để cứu lượt chơi.

`GameEngine.LastGeneration` cho biết số lần thử, số trạng thái đã duyệt, có dùng dự phòng không và lời giải. Không hiển thị lời giải trong giao diện người chơi. `GameEngine.Seed` cùng chuỗi nước đi cho phép tái hiện một ván trong cùng phiên bản engine/runtime:

```csharp
var game = new GameEngine(seed: 2026);
MoveResult move = game.TryPlace(trayIndex: 0, row: 0, column: 0);
if (move.Success)
    Console.WriteLine($"+{move.PointsEarned}; tổng {game.Score}; cấp {game.Level}");
```

Trọng số chi tiết (`n`: số ô, `h`: khoảng trống trong khung bao, `s`: cạnh dài nhất, `D`: level):

```text
target = max(2, 3.2 + 0.18 × min(D−1, 12) − 3.5 × max(0, occupied/64 − 0.45))
spread = 1.35 + 0.07 × (D−1)
bias   = 0.035 × (D−1)
weight = exp(−(n−target)² / (2 × spread²))
       × (1 + bias × (h + max(0, s−3))) / (1 + 0.12 × h)
       × (1 + 0.1 × (D−1) × max(0, n−4))
       × repeatPenalty
```

Bàn chơi dùng `ulong` gồm 64 bit, mỗi bit là một ô. Các mask đặt khối được tính sẵn. Solver nhớ trạng thái `(boardMask, remainingPieces)` và bỏ các nhánh hình/trạng thái tương đương. `HitLimit = true` khi tìm hết ngân sách không có nghĩa khay vô nghiệm. Muốn chỉnh luật: sửa `ShapeCatalog`, `DifficultyProfile`, `ScoreCalculator` và các hệ số trong `PieceGenerator.Choose`.

## Kiểm thử

```powershell
dotnet run --project tests/BlockBlast.Core.Tests -c Release
```

Runner in số ca đã chạy, số ca đạt/thất bại và trả exit code khác 0 khi có lỗi. Có thể kiểm thử engine trên Linux; việc mở cửa sổ, kéo thả thực tế và kiểm tra DPI cần Windows.

## Xuất bản bản chạy Windows

```powershell
dotnet publish src/BlockBlast.WinForms -c Release -r win-x64 --self-contained true -o artifacts/windows-net9
```

Chạy `artifacts/windows-net9/BlockBlast.WinForms.exe`. Bản self-contained chứa runtime .NET 9 nên máy người chơi không cần cài SDK. Muốn hỗ trợ Windows ARM64, đổi `win-x64` thành `win-arm64`.
