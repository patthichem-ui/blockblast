# Kiểm tra game trên Windows

Môi trường cloud dùng Linux: đã dùng SDK .NET 9 để build, chạy kiểm thử logic và publish cho Windows. Các thao tác GUI/thiết bị dưới đây cần thực hiện trên máy Windows thật; không được xem là đã đạt chỉ vì build thành công.

## Chạy và khôi phục ván

1. Giải nén source vào thư mục mới, bấm `Chay-Game.cmd` với SDK 9.0.305/9.0.306 hoặc chạy EXE trong thư mục self-contained đã publish.
2. Đặt ít nhất 4 khối, ghi lại bàn, khay, điểm và số lượt hoàn tác. Đóng rồi mở lại. Bấm Tiếp tục: các giá trị phải giữ nguyên.
3. Hoàn tác, đóng/mở lại; số lượt hoàn tác không được tự tăng. Dùng hết 3 lượt và kiểm tra nút bị vô hiệu hóa.
4. Tạo ván mới, kiểm tra có xác nhận nếu ván hiện tại đang chơi; điểm ván về 0, số lượt hoàn tác về 3 nhưng kỷ lục/cài đặt còn nguyên.
5. Thử mở cửa sổ thứ hai: phải báo game đã mở, không ghi đè tiến độ.

## Giao diện và thao tác

1. Kéo khối bằng một ô ở giữa hình; ô đó phải bám theo con trỏ. Thả ngoài bảng hoặc tại vị trí bị chặn: không mất khối hay điểm.
2. Chọn bằng chuột rồi bấm ô trên bảng; thử `1–3`, mũi tên, `Enter`, `Space`, `Esc`, `H`, `Ctrl+Z`, `P`, `R`, `F1`.
3. Xóa một hàng, nhiều hàng/cột giao nhau và xóa ở hai lượt liên tiếp: xem hiệu ứng, điểm nổi, combo và ô trống sau xóa.
4. Đổi từng theme và sticker; hủy hộp thoại phải giữ cài đặt cũ. Lưu rồi khởi động lại phải giữ lựa chọn mới. Tắt hiệu ứng/sticker phải áp dụng ngay.
5. Thu nhỏ, chuyển cửa sổ, mở cài đặt/thống kê/hướng dẫn: game và nhạc tạm dừng. Trở lại hộp thoại phải giữ đúng trạng thái tạm dừng trước đó.
6. Thử cửa sổ hẹp/rộng, màn hình 1366×768 và DPI 100%/150%/200%; bảng, ba khối và các nút phải truy cập được. Các hộp thoại nhỏ phải cuộn được.

## Âm thanh và dữ liệu

1. Bật nhạc + âm thanh: đặt khối/xóa hàng không được làm mất nhạc nền. Tắt từng công tắc riêng rồi thử lại; mức 0 phải im lặng, tăng lại phải phát được.
2. Tạm dừng/thu nhỏ: âm thanh dừng; tiếp tục phải hoạt động. Thoát game không để lại âm thanh hay tiến trình phát.
3. Nếu không có thiết bị âm thanh, game vẫn phải chơi được.
4. Đóng game, **sao chép toàn bộ `%LOCALAPPDATA%\BlockBlast` sang chỗ khác trước khi thử lỗi dữ liệu**. Làm hỏng `session.json` trong bản dữ liệu dùng thử; mở lại phải báo khôi phục từ `session.json.bak`. Không thử trên ván duy nhất chưa sao lưu.
5. Mở Thống kê: thời gian không tăng khi đã tạm dừng/đóng game; kỷ lục không giảm sau hoàn tác.
