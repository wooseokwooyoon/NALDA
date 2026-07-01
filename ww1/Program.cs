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

   
        const string ProcessName = "ac_client";   
        const int BaseOffset = 0x0018009C;         

        
        static readonly int[] HealthOffsets = { 0xEC };

        
        static readonly int[] AmmoOffsets = { 0x36C, 0x14, 0x0 };

        // 버튼 누르면 채워줄 값
        const int HealthValue = 5000;
        const int AmmoValue = 300;

        
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

        
        IntPtr ResolvePointer(IntPtr handle, IntPtr baseAddr, int[] offsets)
        {
            long addr = ReadInt32(handle, baseAddr);          
            for (int i = 0; i < offsets.Length - 1; i++)
            {
                addr = ReadInt32(handle, (IntPtr)(addr + offsets[i]));  
            }
            return (IntPtr)(addr + offsets[offsets.Length - 1]);        
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
