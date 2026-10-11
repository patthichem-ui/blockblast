# Block Blast · Sticker Studio — C# WinForms

Game xếp khối trên bàn **8 × 8**, viết bằng **C# / .NET 9 / Windows Forms**. Đây là bộ luật và thuật toán tự triển khai cho một game kiểu Block Blast; có thể đọc, sửa và dùng làm nền cho dự án của bạn.

## Bản nâng cấp Sticker Studio

- **Đóng rồi mở lại vẫn tiếp tục đúng ván:** lưu bàn chơi, ba khối còn lại, điểm, combo, cấp độ, màu/sticker từng ô, lịch sử hoàn tác và trạng thái sinh khối tiếp theo.
- **4 theme nền:** Cực quang, Kẹo ngọt, Đại dương, Hoàng hôn; **3 bộ sticker vẽ vector:** Kawaii (mèo/thỏ), Cosmic (sao/trăng), Garden (hoa).
- Khối bo góc có ánh sáng, hình xem trước khi kéo thả, hạt sao khi xóa hàng, điểm nổi và thông báo combo; giao diện tự bố trí theo kích thước cửa sổ.
- Nhạc nền tổng hợp riêng và tiếng đặt/xóa khối, combo, hoàn tác, kết thúc ván. Có thể bật/tắt **riêng** nhạc, âm thanh, hiệu ứng chuyển động và sticker; có thanh âm lượng chung. Không cần tải file nhạc từ mạng.
- **3 lần hoàn tác mỗi ván**, gợi ý vị trí đặt hợp lệ, tạm dừng/tiếp tục, thống kê thành tích và thời gian chơi. Chuyển sang ứng dụng khác hoặc thu nhỏ cửa sổ sẽ tự tạm dừng.
- Giao diện, luật chơi, lưu trữ và âm thanh được tách thành các lớp có trách nhiệm riêng; các dịch vụ có thể thay bằng implementation khác qua interface.

Đây là game desktop độc lập lấy cảm hứng từ cách chơi xếp khối, không phải source hoặc tài nguyên của ứng dụng thương mại trên CH Play. Bản hiện tại tập trung vào chơi offline; chưa có tài khoản, đồng bộ đám mây hay bảng xếp hạng online.

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
- Phím `1`, `2`, `3` chọn khối; mũi tên chọn ô; `Enter`/`Space` đặt; `Esc` hủy chọn; `H` gợi ý; `Ctrl+Z` hoàn tác; `P` tạm dừng/tiếp tục; `R` ván mới; `F1` hướng dẫn. `Esc` khi chưa chọn khối cũng tạm dừng/tiếp tục.
- Khối không được chồng lên ô đã có hoặc vượt ra ngoài bàn. Khối đã sinh giữ nguyên hướng, không xoay trong lượt chơi.
- Hàng và cột đầy được xóa **đồng thời**. Ô giao nhau chỉ được xóa một lần nhưng cả hàng và cột đều được tính điểm.
- Dùng hết cả ba khối mới nhận khay tiếp theo. Game kết thúc khi **mọi khối còn lại** đều không có vị trí đặt hợp lệ.
- Bấm **Tùy chỉnh** để đổi theme, sticker và âm thanh; bấm **Thống kê** để xem kỷ lục, combo tốt nhất, tổng khối/hàng đã xóa và thời gian chơi.
- Khi game over vẫn có thể hoàn tác nếu còn lượt. Gợi ý không đặt khối thay bạn; bấm `Enter` hoặc chọn vị trí khác để tiếp tục.

## Lưu tiến độ và cài đặt

Dữ liệu nằm trong **`%LOCALAPPDATA%\BlockBlast`** của tài khoản Windows hiện tại, dùng chung khi bạn chuyển source sang thư mục mới:

| Tệp | Nội dung |
| --- | --- |
| `session.json` | Ván chơi, cài đặt, thống kê, lịch sử và lượt hoàn tác còn lại |
| `session.json.bak` | Bản lưu hợp lệ trước lần ghi gần nhất |
| `*.corrupt-*` | Bản dữ liệu hỏng được giữ lại để kiểm tra khi phát hiện lỗi |

Game tự lưu sau mỗi lượt đặt thành công, hoàn tác, tạo ván mới, đổi cài đặt, tạm dừng/tiếp tục; đồng thời lưu mỗi 30 giây và khi đóng cửa sổ. Khi mở lại sau lần đóng bình thường, bấm **Tiếp tục** hoặc `P` để chơi tiếp. Không tính thời gian khi tạm dừng, game over hoặc ứng dụng đã đóng.

Tệp mới được ghi vào file tạm và flush trước khi thay thế bản chính; bản chính lỗi sẽ được thử khôi phục từ backup. Nếu máy không ghi được dữ liệu, giao diện thông báo lỗi và hỏi trước khi thoát để tránh mất các lượt chưa lưu. Mỗi phiên đăng nhập Windows chỉ mở một cửa sổ game để tránh hai phiên ghi đè tiến độ của nhau.

Kỷ lục từ `highscore.json` của bản cũ được đọc và giữ lại. Kỷ lục, tổng số lượt đặt/xóa hàng và combo tốt nhất không bị trừ khi hoàn tác; số ván kết thúc chỉ tính một lần cho mỗi ván, kể cả khi hoàn tác rồi game over lần nữa. Tạo ván mới thay thế ván hiện tại, giữ lại thống kê và cài đặt.

## Cấu trúc source

```text
src/BlockBlast.Core/                  Luật chơi, sinh khay, điểm/độ khó, snapshot, undo
src/BlockBlast.Application/           Điều phối phiên chơi, lưu trữ, cài đặt, thống kê
src/BlockBlast.WinForms/
  MainForm.cs                        Vòng đời cửa sổ và điều phối lệnh
  GameSurface*.cs                     Canvas, vẽ bảng, kéo thả và bàn phím
  Controls/ · Dialogs/                Nút, công tắc, tùy chỉnh, thống kê, hướng dẫn
  Rendering/ · Themes/ · Effects/     Vẽ vector, bố cục, palette và hoạt ảnh
  Audio/                             Tổng hợp PCM, trộn nhạc/hiệu ứng, thiết bị WinMM
tests/BlockBlast.Core.Tests/          Luật, solver, trạng thái lưu/undo và RNG
tests/BlockBlast.Application.Tests/   Tệp lưu, phục hồi, thống kê và vòng đời phiên
tests/BlockBlast.Audio.Tests/         PCM, mixer và bật/tắt âm thanh
tests/BlockBlast.Presentation.Tests/  Bố cục, khay và ánh xạ con trỏ ở nhiều DPI
```

`GameEngine` sở hữu trạng thái và chỉ áp dụng luật chơi. `GameSessionController` điều phối engine, thống kê và tự lưu thông qua `IGameRepository`; `JsonGameRepository` chịu trách nhiệm ghi/đọc dữ liệu. `MainForm` và `GameSurface` gọi controller, không tự tính điểm hay sửa bàn chơi. `IGameAudio` tách giao diện khỏi thiết bị âm thanh; `GameAudioService`, `AudioMixer`, `PcmComposer` và `WaveOutDevice` tách điều phối, trộn âm, tạo âm và phát âm.

Core/Application không phụ thuộc WinForms, nên có thể kiểm thử độc lập và dùng lại cho giao diện C# khác. `TimeProvider` có thể được truyền vào controller để kiểm thử thời gian mà không phải chờ thật.

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

`GameEngine.LastGeneration` cho biết số lần thử, số trạng thái đã duyệt, có dùng dự phòng không và lời giải. Nút gợi ý tìm một nước đi hợp lệ trong khay hiện tại, ưu tiên một lời giải dùng hết khay khi solver tìm được. `GameEngine.Seed` cùng chuỗi nước đi cho phép tái hiện ván trong cùng phiên bản engine; `StatefulRandom` lưu trạng thái RNG để mở lại hoặc hoàn tác không làm đổi các khối sắp sinh:

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
dotnet run --project tests/BlockBlast.Application.Tests -c Release
dotnet run --project tests/BlockBlast.Audio.Tests -c Release
dotnet run --project tests/BlockBlast.Presentation.Tests -c Release
```

Các runner console không cần thư viện test ngoài, in số ca đạt/thất bại và trả exit code khác 0 khi có lỗi. Core kiểm tra xóa giao hàng/cột, combo, độ khó, ngân sách solver, khay có nghiệm, snapshot và hoàn tác qua lượt sinh khay. Application kiểm tra đọc lại sau khi khởi động tiến trình mới, file hỏng/backup, quyền ghi, di chuyển kỷ lục, cài đặt và thời gian tạm dừng. Audio kiểm tra mẫu âm hợp lệ, phát đồng thời nhạc/hiệu ứng và các công tắc độc lập. Presentation kiểm tra hình học bố cục ở nhiều kích thước/DPI, khối nằm gọn trong khay và tọa độ con trỏ khớp ô vẽ.

Build và các kiểm thử này chạy được trên Linux. Chúng không thay thế việc mở cửa sổ và nghe loa trên Windows; checklist kiểm tra thực tế nằm trong [docs/Kiem-tra-Windows.md](docs/Kiem-tra-Windows.md).

## Xuất bản bản chạy Windows

```powershell
dotnet publish src/BlockBlast.WinForms -c Release -r win-x64 --self-contained true -o artifacts/windows-deluxe-net9
```

Chạy `artifacts/windows-deluxe-net9/BlockBlast.WinForms.exe`. Gửi **toàn bộ thư mục publish**, không chỉ file EXE. Bản self-contained chứa runtime .NET 9 nên máy người chơi không cần cài SDK. Muốn hỗ trợ Windows ARM64, đổi `win-x64` thành `win-arm64`.
