using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AssaultCubeTrainer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        // =========================================================
        //  Windows API (P/Invoke) - 다른 프로세스 메모리에 접근하는 함수들
        // =========================================================
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(
            IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

        // =========================================================
        //  게임 / 포인터 정보  (치트엔진에서 찾은 값 그대로)
        // =========================================================
        const string ProcessName = "ac_client";   // ac_client.exe  (".exe"는 빼고 적음)
        const int BaseOffset = 0x0018009C;         // "ac_client.exe" + 0x0018009C  (정적 베이스)

        // 체력:  [[ac_client.exe+0018009C] + EC]
        static readonly int[] HealthOffsets = { 0xEC };

        // 탄약:  [[[[ac_client.exe+0018009C] + 36C] + 14] + 0]
        static readonly int[] AmmoOffsets = { 0x36C, 0x14, 0x0 };

        // 버튼 누르면 채워줄 값
        const int HealthValue = 5000;
        const int AmmoValue = 300;

        // =========================================================
        //  UI 구성 요소
        // =========================================================
        Button btnHealth;
        Button btnAmmo;
        Label lblStatus;

        public MainForm()
        {
            Text = "AssaultCube 트레이너";
            Size = new Size(360, 230);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("맑은 고딕", 10F);

            btnHealth = new Button
            {
                Text = "체력 충전",
                Size = new Size(140, 60),
                Location = new Point(20, 20)
            };
            btnHealth.Click += (s, e) => SetValue(HealthOffsets, HealthValue, "체력");

            btnAmmo = new Button
            {
                Text = "탄약 충전",
                Size = new Size(140, 60),
                Location = new Point(180, 20)
            };
            btnAmmo.Click += (s, e) => SetValue(AmmoOffsets, AmmoValue, "탄약");

            lblStatus = new Label
            {
                Text = "AssaultCube를 실행한 뒤 버튼을 누르세요.",
                Location = new Point(20, 110),
                Size = new Size(310, 70),
                TextAlign = ContentAlignment.TopLeft
            };

            Controls.Add(btnHealth);
            Controls.Add(btnAmmo);
            Controls.Add(lblStatus);
        }

        // =========================================================
        //  포인터 체인 해석
        //  baseAddr 위치에 들어있는 값을 읽고, 마지막 오프셋 전까지는
        //  계속 역참조(ReadInt32)한 뒤, 마지막 오프셋은 더하기만 한다.
        // =========================================================
        IntPtr ResolvePointer(IntPtr handle, IntPtr baseAddr, int[] offsets)
        {
            long addr = ReadInt32(handle, baseAddr);          // 1단계: 베이스가 가리키는 주소
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                addr = ReadInt32(handle, (IntPtr)(addr + offsets[i]));  // 중간 오프셋은 역참조
            }
            return (IntPtr)(addr + offsets[offsets.Length - 1]);        // 마지막 오프셋은 더하기만
        }

        int ReadInt32(IntPtr handle, IntPtr addr)
        {
            byte[] buf = new byte[4];
            ReadProcessMemory(handle, addr, buf, 4, out _);
            return BitConverter.ToInt32(buf, 0);
        }

        void WriteInt32(IntPtr handle, IntPtr addr, int value)
        {
            byte[] buf = BitConverter.GetBytes(value);
            WriteProcessMemory(handle, addr, buf, 4, out _);
        }

        // =========================================================
        //  실제 동작: 프로세스 찾기 → 핸들 열기 → 주소 계산 → 값 쓰기
        // =========================================================
        void SetValue(int[] offsets, int value, string name)
        {
            Process[] procs = Process.GetProcessesByName(ProcessName);
            if (procs.Length == 0)
            {
                lblStatus.Text = "게임을 찾을 수 없습니다.\nAssaultCube를 먼저 실행하세요.";
                return;
            }

            Process game = procs[0];
            IntPtr handle = OpenProcess(PROCESS_ALL_ACCESS, false, game.Id);
            if (handle == IntPtr.Zero)
            {
                lblStatus.Text = "프로세스를 열 수 없습니다.\n이 프로그램을 '관리자 권한'으로 실행하세요.";
                return;
            }

            try
            {
                // ac_client.exe 모듈의 시작 주소 + 정적 오프셋 = 정적 베이스
                long moduleBase = game.MainModule.BaseAddress.ToInt64();
                IntPtr baseAddr = (IntPtr)(moduleBase + BaseOffset);

                IntPtr finalAddr = ResolvePointer(handle, baseAddr, offsets);
                WriteInt32(handle, finalAddr, value);

                lblStatus.Text = $"{name} = {value} 적용 완료!\n(최종 주소: 0x{(long)finalAddr:X})";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "오류: " + ex.Message;
            }
            finally
            {
                CloseHandle(handle);
            }
        }
    }
}
