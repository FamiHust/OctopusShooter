# TÀI LIỆU NỘP BÀI DỰ THI - DỰ ÁN: OCTOPUS SHOOTER

---

## 📋 THÔNG TIN CHUNG
- **Tên dự án:** Octopus Shooter (Flow Blast Shooter)
- **Thể loại:** Casual / Hybrid-Casual Puzzle & Color Sorting
- **Nền tảng:** Mobile (Android)
- **IP Nhân vật chính:** **Chú Bạch Tuộc (Octopus)** – Thiết kế 3D Stylized/Toony dễ thương, sử dụng vòi (tentacles) linh hoạt để bắn bóng nước và tương tác với các khối block màu.

---

## I. MÔ TẢ SƠ QUAN VỀ IDEA CỦA GAME

### 1. Bối cảnh & Cốt truyện (Theme & Lore)
* **Octopus Shooter** lấy bối cảnh tại Vương quốc Đại dương nhộn nhịp. Một ngày nọ, những dải màu & khối chướng ngại vật tràn dạt vào các nhánh băng chuyền vòng tròn, làm tắc nghẽn dòng chảy năng lượng biển.
* Đội quân **Bạch Tuộc (Octopus IP)** đủ sắc màu rực rỡ (Đỏ, Xanh, Vàng,...) nằm xếp lớp trên các ô lưới bên dưới. Nhiệm vụ của người chơi là chọn và chuyển các chú Bạch tuộc lên hàng chờ để bắn dọn dẹp sạch sẽ các dải màu trên băng chuyền.

### 2. Ý tưởng Gameplay chính (Core Gameplay Loop)
* Người chơi quan sát bàn cờ lưới ô vuông (Grid) bên dưới để tìm các chú Bạch tuộc ở trạng thái **tự do** (không bị chặn cả 4 hướng).
* Người chơi chạm để đẩy chú Bạch tuộc lên **Hàng chờ** (tối đa 4 ô).
* Băng chuyền liên tục xả các dải màu running vòng quanh. Khi dải màu chạy ngang qua chú Bạch tuộc khớp màu ở Hàng chờ, chú Bạch tuộc sẽ tự động bắn để tiêu diệt dải màu, trừ dần đạn về 0 và biến mất để giải phóng ô chờ.

---

## II. CORE MECHANIC: SORTING & BĂNG CHUYỀN (CONVEYOR & SORTING MECHANICS)

Cơ chế cốt lõi của game là sự kết hợp chặt chẽ giữa **Phân loại di chuyển, **Quản lý Hàng chờ** và **Băng chuyền**.

```
                  [Hệ thống Băng Chuyền]
               (Xả dải màu / block màu chạy liên tục)
                                 ▲
                     (Bắn khớp màu)
                                 │
                 [Hàng Chờ (4 ô)]
                                 ▲
                (Chạm đẩy Bạch Tuộc tự do)
                                 │
            [Bàn cờ Lưới ô vuông (Grid chứa Bạch Tuộc)]
```

### 1. Chi tiết Quy trình Gameplay

* **Bước 1: Quan sát & Đẩy Bạch Tuộc**
  * Người chơi quan sát lưới ô vuông bên dưới để tìm chú Bạch tuộc đang ở trạng thái **tự do** (không bị tường, hầm hoặc Bạch tuộc khác chặn ở cả 4 hướng).
  * Người người chơi chạm để chuyển chú Bạch tuộc đó từ lưới lên 1 trong 4 vị trí thuộc **Hàng chờ**.

* **Bước 2: Xếp hàng & Chờ Dải màu**
  * Bạch tuộc đứng chiếm giữ 1 vị trí (Slot) ở Hàng chờ (Tối đa 4 ô).
  * Lúc này, dung lượng Hàng chờ bị thu hẹp lại. Người chơi phải tính toán kỹ lưỡng sao cho không lấp đầy cả 4 ô nếu chưa có dải màu tương ứng xuất hiện trên băng chuyền.

* **Bước 3: Khớp Màu & Tiêu thụ Đạn**
  * Băng chuyền liên tục được đổ đầy từ các nhánh băng chuyền ở góc trên, xả các dải màu chạy vòng quanh băng chuyền.
  * Khi dải màu chạy ngang qua chú Bạch tuộc ở Hàng chờ có màu trùng khớp, Bạch tuộc sẽ tự động hút/bắn dải màu đó vào và trừ dần chỉ số đạn (từ 100 về 0).

* **Bước 4: Giải phóng Vị trí & Qua Level (Slot Clearing & Progression)**
  * Khi chỉ số đạn của Bạch tuộc giảm về 0, Bạch tuộc sẽ biến mất và giải phóng lại ô trống cho Hàng chờ.
  * Người chơi lặp lại quá trình này cho đến khi dọn dẹp sạch sẽ toàn bộ Bạch tuộc trên lưới để giành **Chiến thắng**.

---

### 2. Điều kiện Thắng & Thua (Win / Lose Conditions)

| Trạng thái | Điều kiện |
| :--- | :--- |
| **🏆 THẮNG (Victory)** | Dọn dẹp toàn bộ Bạch tuộc trên Lưới bên dưới và không còn Bạch tuộc nào ở Hàng chờ. |
| **☠️ THUA (Defeat)** | Toàn bộ 4 Slot Hàng chờ đều bị lấp đầy và dòng màu chạy qua băng chuyền không khớp với bất kỳ Bạch tuộc nào trong hàng chờ (**Đóng băng dòng chảy - Deadlock**). |

---

### 3. Hệ thống Booster Điều Kiện & Băng Chuyền Phụ (Special Boosters)

* **💎 Magic Stone (Đá Thần Kỳ - Booster tích lũy):**
  * *Điều kiện kích hoạt:* Bắn liên tiếp 50 block cùng màu $\rightarrow$ Nhận 1 viên đá.
  * *Tác dụng:* Thu thập đủ 3 viên đá để Clear sạch toàn bộ dải màu/block đang có trên băng chuyền vòng tròn.
* **🌀 Portal Shooter (Cổng Dịch Chuyển):**
  * *Chỉ xuất hiện tại các level có băng chuyền phụ.*
  * *Tác dụng:* Hút toàn bộ Block ở 2 băng chuyền phụ và nhả ra lại các block đó nhưng đảo lại vị trí màu, hỗ trợ người chơi gỡ rối trong các tình huống Deadlock.

---

### 4. Đa dạng hóa Gameplay (Special Obstacles)
* **Bạch tuộc đặc biệt trên Grid:**
  * *Ice Octopus (Bạch tuộc đóng băng):* Bị đóng băng trên lưới, cần giải phóng các ô xung quanh hoặc giải đông trước khi có thể di chuyển.
  * *Hidden Shooter (Bạch tuộc ẩn màu):* Bạch tuộc bị che khuất màu sắc, người chơi chỉ nhận diện được màu chính xác sau khi chọn đẩy xuống Hàng chờ hoặc mở khóa lối đi.
  * *Tunnel Octopus (Bạch tuộc Hầm):* Bạch tuộc nằm sâu trong hầm/ống chui, đẩy bạch tuộc lên dần khi không có bạch tuộc/Obstacles nào chắn trước mặt.

---

## III. KẾ HOẠCH VÀ TIẾN ĐỘ PHÁT TRIỂN GAME

### Phase 1: Prototype & Core Mechanics (Đã Hoàn Thành)
- [x] Logic tìm & kiểm tra Bạch tuộc tự do trên Grid.
- [x] Cơ chế đẩy Bạch tuộc lên 4 Slot Hàng chờ.
- [x] Hệ thống Băng chuyền vòng tròn và băng chuyền phụ di chuyển dải màu liên tục.
- [x] Logic Match màu, trừ đạn về 0 & xử lý Thắng/Thua (Deadlock).

### Phase 2: Feature Expansion & Level Systems (Đang Thực Hiện)
- [x] Phát triển Level Editor hỗ trợ cấu hình Grid & nhánh Băng chuyền phụ.
- [x] Hệ thống Booster (Magic Stone, Portal Shooter cho băng chuyền phụ, Swap, Extra Slot).
- [x] Hiệu ứng visual (Custom Water Shader, Toony Mesh Skinning).

### Phase 3: Visual Polish, UI/UX & Sound
- [ ] Rigging & Animation chuyển động vòi hút/bắn cho Bạch tuộc.
- [ ] Tối ưu hóa UI/UX màn hình Gameplay, Shop, Victory/Defeat.
- [ ] Tích hợp Âm thanh (VFX Sound, Background Music, Combo ASMR Sound).

### Phase 4: Balancing, Testing & Release
- [ ] Playtest cân bằng độ khó chuỗi màn chơi.
- [ ] Tối ưu dung lượng & FPS trên thiết bị Android.
- [ ] Đóng gói bản Final nộp bài dự thi.
