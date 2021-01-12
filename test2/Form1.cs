using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Media;

namespace gmc4
{
    public partial class GMC4Sim : Form
    {
        private string fileName = "";
        private GMC4 gmc4 = new GMC4();


        private byte address = 0;
        // 実行フラグ
        private bool exeFlag = false;

        private bool isRunning = false;
        private bool isChanged = false;
        private bool isRealtimeAssemble = false;

        private static PictureBox[] binLEDs = new PictureBox[7];
        private static PictureBox[] segLEDs = new PictureBox[7];
        private static SoundPlayer tapB;
        private static SoundPlayer shortB;
        private static SoundPlayer longB;
        private static SoundPlayer endB;
        private static SoundPlayer errorB;
        private static SoundPlayer[] sund;

        private int key = -1;
        private Stack<int> lastPushKey = new Stack<int>();

        private static Command[] cmds = {
             new Command("KA",0x0),
             new Command("AO",0x1),
             new Command("CH",0x2),
             new Command("CY",0x3),
             new Command("AM",0x4),
             new Command("MA",0x5),
             new Command("M+",0x6),
             new Command("M-",0x7),
             new Command("TIA",0x8,1),
             new Command("AIA",0x9,1),
             new Command("TIY",0xA,1),
             new Command("AIY",0xB,1),
             new Command("CIA",0xC,1),
             new Command("CIY",0xD,1),
             new Command("JUMP",0xF,2),
             new Command("CAL",0xE,new Command[15]{
                 new Command("RSTO",0x0),
                 new Command("SETR",0x1),
                 new Command("RSTR",0x2),
                 new Command("CMPL",0x4),
                 new Command("CHNG",0x5),
                 new Command("SIFT",0x6),
                 new Command("ENDS",0x7),
                 new Command("ERRS",0x8),
                 new Command("SHTS",0x9),
                 new Command("LONS",0xA),
                 new Command("SUND",0xB),
                 new Command("TIMR",0xC),
                 new Command("DSPR",0xD),
                 new Command("DEM-",0xE),
                 new Command("DEM+",0xF)
             })
        };

        public GMC4Sim()
        {
            InitializeComponent();

            binLEDs = new PictureBox[7] { binLED0, binLED1, binLED2, binLED3, binLED4, binLED5, binLED6 };
            segLEDs = new PictureBox[7] { Aseg, Bseg, Cseg, Dseg, Eseg, Fseg, Gseg };

            tapB = new SoundPlayer(Properties.Resources.tap);
            shortB = new SoundPlayer(Properties.Resources._short);
            longB = new SoundPlayer(Properties.Resources._long);
            endB = new SoundPlayer(Properties.Resources.end);
            errorB = new SoundPlayer(Properties.Resources.error);
            sund = new SoundPlayer[]
            {
                new SoundPlayer(Properties.Resources.A3),
                new SoundPlayer(Properties.Resources.B3),
                new SoundPlayer(Properties.Resources.C4),
                new SoundPlayer(Properties.Resources.D4),
                new SoundPlayer(Properties.Resources.E4),
                new SoundPlayer(Properties.Resources.F4),
                new SoundPlayer(Properties.Resources.G4),
                new SoundPlayer(Properties.Resources.A4),
                new SoundPlayer(Properties.Resources.B4),
                new SoundPlayer(Properties.Resources.C5),
                new SoundPlayer(Properties.Resources.D5),
                new SoundPlayer(Properties.Resources.E5),
                new SoundPlayer(Properties.Resources.F5),
                new SoundPlayer(Properties.Resources.G5),
            };
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            NewFile();
            AddressChange();
            MemoryTextChange();
            Set7SegLED(gmc4.MemGet(address));

            this.ActiveControl = SourceText;
            SourceText.SelectionStart = 0;
        }

        // 2進LEDを更新する
        private void SetBinaryLED(int a)
        {
            for (int i = 0; i < binLEDs.Length; i++, a >>= 1)
                binLEDs[i].BackColor = (a % 2 == 1) ? Color.Red : Color.Black;
        }

        // 7セグLEDを更新する（-1で消灯）
        private void Set7SegLED(int a)
        {
            // aをビットごとに分ける
            int[] b = { (a >> 3) & 0x1, (a >> 2) & 0x1, (a >> 1) & 0x1, (a >> 0) & 0x1 };
            int[] c = new int[7];
            if (a >= 0x0 && a <= 0xF)
            {
                c[0] = ~b[1] & ~b[3] | ~b[0] & b[2] | ~b[0] & b[1] & b[3] | b[1] & b[2] | b[0] & ~b[1] & ~b[2] | b[0] & ~b[3];
                c[1] = ~b[0] & ~b[1] | ~b[0] & ~b[2] & ~b[3] | ~b[1] & ~b[3] | ~b[0] & b[2] & b[3] | b[0] & ~b[2] & b[3];
                c[2] = ~b[0] & ~b[2] | ~b[0] & b[3] | ~b[2] & b[3] | ~b[0] & b[1] | b[0] & ~b[1];
                c[3] = ~b[0] & ~b[1] & ~b[3] | ~b[1] & b[2] & b[3] | b[1] & ~b[2] & b[3] | b[1] & b[2] & ~b[3] | b[0] & ~b[2];
                c[4] = ~b[1] & ~b[3] | b[2] & ~b[3] | b[0] & b[2] | b[0] & b[1];
                c[5] = ~b[2] & ~b[3] | ~b[0] & b[1] & ~b[2] | b[1] & ~b[3] | b[0] & ~b[1] | b[0] & b[2];
                c[6] = ~b[1] & b[2] | b[2] & ~b[3] | ~b[0] & b[1] & ~b[2] | b[0] & ~b[1] | b[0] & b[3];
            }
            for (int i = 0; i < 7; i++)
                segLEDs[i].BackColor = ((c[i] & 0x1) == 1) ? Color.Red : Color.Black;
        }

        // 新規ファイルを作成時に行うリセット
        private void NewFile()
        {
            if (CheckSourceDestroy()) // 更新内容を破棄してもよい場合
            {
                Stop();

                SourceText.Text = "\tSTART\r\n\tRET\r\n\tEND";
                isChanged = false;
                fileName = "";
                ChangeWindowTitle();
                LogLabelStatusStrip.Text = "新規ファイルを作成しました。";
            }
        }

        // メモリの内容を表示させる処理
        private void MemoryTextChange()
        {
            string str = "";
            for (int i = 0; i < 0x70; i++)
                str += IntToChar(gmc4.Mem[i]);
            memory.Text = str;
        }

        // 2進LED・7セグLEDを変更する処理
        private void AddressChange()
        {
            NowAddressToolStripStatusLabel.Text = $"現在のアドレス：0x{address:X2}";
            if (!isRunning)
            {
                SetBinaryLED(address);
                Set7SegLED(gmc4.MemGet(address));
            }
        }

        // コードを実行するときの処理
        private void Start(bool isStepRun = false)
        {
            address = 0;
            isRunning = true;
            if (!isStepRun) timer1.Start();

            RunStatusStrip.Text = $"実行中{(isStepRun ? "(ステップ実行)" : "")}";
            LogLabelStatusStrip.Text = $"プログラムを{(isStepRun ? "ステップ" : "")}実行しました。";
            statusStrip1.BackColor = Color.LightSalmon;
            Set7SegLED(-1);
        }

        // コードの実行を終了する処理
        private void Stop()
        {
            timer1.Stop();

            isRunning = false;

            RunStatusStrip.Text = "待機中";
            LogLabelStatusStrip.Text = "プログラムを停止しました。";
            statusStrip1.BackColor = Color.LightSkyBlue;

            address = 0;
            AddressChange();
        }

        #region メニューアイテム関係
        private void ChangeWindowTitle()
        {
            string text = "";
            if (isChanged) text += "*";
            if (fileName == "") text += "無題";
            else text += fileName;
            text += " - GMC4 シミュレータ";
            Text = text;
        }

        private bool CheckSourceDestroy()
        {
            if (isChanged)
            {
                var result = MessageBox.Show("変更内容を破棄しますか？", "GMC4 シミュレータ", MessageBoxButtons.OKCancel, MessageBoxIcon.None, MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Cancel) return false;
            }
            return true;
        }
        // 新規作成時の処理
        private void NewToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NewFile();
        }

        // ファイルを開いた時の処理
        private void OpenOToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CheckSourceDestroy())
            {
                if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                Stop();

                StreamReader sr = new StreamReader(openFileDialog.FileName, Encoding.GetEncoding("shift_jis"));
                SourceText.Text = sr.ReadToEnd();
                sr.Close();

                isChanged = false;
                fileName = openFileDialog.FileName;
                ChangeWindowTitle();
                OwSaveSToolStripMenuItem.Enabled = true;

                Assemble();

                LogLabelStatusStrip.Text = fileName + "を開きました。";
            }
        }

        // ファイルを上書き保存するときの処理
        private void OwSaveSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StreamWriter sw = new StreamWriter(fileName, false, Encoding.GetEncoding("shift_jis"));
            sw.Write(SourceText.Text);
            sw.Close();

            isChanged = false;
            ChangeWindowTitle();

            LogLabelStatusStrip.Text = fileName + "に上書き保存しました。";
        }

        // ファイルを名前を付けて保存するときの処理
        private void SaveSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

            StreamWriter sw = new StreamWriter(saveFileDialog.FileName, false, Encoding.GetEncoding("shift_jis"));
            sw.Write(SourceText.Text);
            sw.Close();

            isChanged = false;
            fileName = saveFileDialog.FileName;
            ChangeWindowTitle();
            OwSaveSToolStripMenuItem.Enabled = true;

            LogLabelStatusStrip.Text = fileName + "に保存しました。";
        }

        // 終了するときの処理
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CheckSourceDestroy())
                this.Close();
        }

        // リアルタイムアセンブルをするかの変更処理
        private void IsRealtimeAssembleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            isRealtimeAssemble = !isRealtimeAssemble;
            IsRealtimeAssembleToolStripMenuItem.CheckState = isRealtimeAssemble ? CheckState.Checked : CheckState.Unchecked;
            LogLabelStatusStrip.Text = $"リアルタイムアセンブルを{(isRealtimeAssemble ? "有効化" : "無効化")}しました。";
        }
        #endregion

        #region ボタンクリック関係

        private void AsetBtn_Click(object sender, EventArgs e)
        {
            try
            {
                int ad = 0;
                for (int i = 0; i < 2; i++) ad |= lastPushKey.Pop() << (i * 4);
                if (ad >= 0x60) throw new GMC4Exception("指定したアドレスには移動できません。");
                address = (byte)ad;
                AddressChange();

                LogLabelStatusStrip.Text = $"0x{address:X2}番地にカーソルを移動しました。";
            }
            // Stackが空だった場合は無視
            catch (InvalidOperationException) { }
            // 指定アドレスが無効だった場合ログ出力
            catch (GMC4Exception exp)
            {
                LogLabelStatusStrip.Text = exp.Message;
            }
            finally
            {
                lastPushKey.Clear();
                TapBeep();
            }
        }

        private void InclBtn_Click(object sender, EventArgs e)
        {
            if (lastPushKey.Count > 0)
            {


                Int4 key = lastPushKey.Pop();
                gmc4.MemSet(address, key);
                MemoryTextChange();
                LogLabelStatusStrip.Text = $"0x{address:X2}番地に{key}を書き込みました。";
            }
            lastPushKey.Clear();

            address++;
            AddressChange();

            TapBeep();

        }

        private void RunBtn_Click(object sender, EventArgs e)
        {
            if (lastPushKey.Count > 0 && lastPushKey.Pop() == 1)
            {
                Start();
            }
            else
            {
                LogLabelStatusStrip.Text = "プログラムを実行するには'1→RUN'を押してください。";
            }
            lastPushKey.Clear();
            TapBeep();
        }

        private void ResetBtn_Click(object sender, EventArgs e)
        {
            Stop();
            TapBeep();
        }

        private void AssembleBtn_Click(object sender, EventArgs e)
        {
            Assemble();
        }
        #endregion

        #region サウンド関係
        private void TapBeep()
        {
            tapB.PlaySync();
        }

        #endregion
        // キーを押した時の処理
        private void Key_Down(object sender, MouseEventArgs e)
        {
            key = CharToInt(((Button)sender).Text[0]);
            lastPushKey.Push(key);
            if (!isRunning)
            {
                Set7SegLED(key);
                LogLabelStatusStrip.Text = $"キー{key}が押されました。";
                if (key == 1) LogLabelStatusStrip.Text += "RUNキーを押すとプログラムを実行します。";
                TapBeep();
            }
            //Console.WriteLine(key);
        }

        // キーを離したときの処理
        private void Key_Up(object sender, MouseEventArgs e)
        {
            key = -1;
        }

        // ソーステキストが変更された時の処理
        private void SourceText_TextChanged(object sender, EventArgs e)
        {
            isChanged = true;
            ChangeWindowTitle();
            if (isRealtimeAssemble)
                AssembleBtn_Click(sender, e);
        }

        // アセンブル処理
        private void Assemble()
        {
            try
            {
                // 行ごとに分割（空白行は無視する）
                var texts = SourceText.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < texts.Length; i++)
                {
                    int delIdx = texts[i].IndexOf(';');
                    if (delIdx >= 0) texts[i] = texts[i].Substring(0, delIdx);
                    texts[i] = texts[i].TrimEnd(); // 末尾の空白文字を削除
                    texts[i] = texts[i].ToUpper(); // 小文字を大文字に変換
                }
                char[] del = { ' ', '\t' };

                // StartとEndの行を取得
                int startLine = -1;
                int endLine = -1;
                for (int i = 0; i < texts.Length; i++)
                {
                    string[] term = texts[i].Split(del);
                    if (term.Length < 2) continue; // そもそも命令が無かったらcontine
                    if (term[0] == "" && term[1] == "START") startLine = i;
                    if (startLine != -1 && term[0] == "" && term[1] == "END")
                    {
                        endLine = i;
                        break;
                    }
                }
                if (startLine == -1) throw new GMC4Exception("STARTがありません。");
                if (endLine == -1) throw new GMC4Exception("ENDがありません。");

                List<Label> labelList = new List<Label>();
                byte adr = 0;

                // パス1の処理（ラベルの解決）
                for (int i = startLine + 1; i < endLine; i++)
                {
                    string[] term = texts[i].Split(del);
                    if (term.Length >= 2) // オペコードがあるならば
                    {
                        if (term[0] != "") // ラベルがあった時
                            labelList.Add(new Label(term[0], adr));
                        int idx = Array.FindIndex(cmds, (ele) => ele.symbol == term[1]);
                        if (idx != -1)
                            adr += (byte)(1 + cmds[idx].argNum);
                    }
                }

                // パス2の処理（アセンブル）
                adr = 0;
                int dataOffset = 0;
                GMC4 gmc4_m = new GMC4();
                for (int i = startLine + 1; i < endLine; i++)
                {
                    string[] term = texts[i].Split(del);

                    if (term.Length >= 2)
                    {
                        string label = term[0];
                        string opc = term[1];

                        // データメモリ書き込み開始
                        if (opc == "RET") dataOffset = 0;
                        // データメモリ書き込み
                        else if (opc == "DC" && dataOffset >= 0)
                        {
                            if (term.Length < 3) throw new GMC4Exception("オペランドがありません。");
                            char opr = term[2][0];
                            gmc4_m.DataSet(dataOffset++, opr);
                        }
                        // その他命令処理
                        else
                        {
                            int idx = Array.FindIndex(cmds, (ele) => ele.symbol == opc);
                            if (idx == -1) throw new GMC4Exception("オペコードがありません。");
                            Command cmd = cmds[idx];
                            Console.WriteLine(cmd.symbol);

                            gmc4_m.MemSet(adr++, cmd.code);

                            if (term.Length > 2 + cmd.argNum)
                                throw new GMC4Exception($"オペランドが多すぎます。");
                            // サブルーチンの場合
                            else if (cmd.subCmds.Length != 0)
                            {
                                if (term.Length != 3) throw new GMC4Exception("サブルーチン命令が正しくありません。");
                                string subSymbol = term[2];
                                int idx2 = Array.FindIndex(cmd.subCmds, (ele) => ele.symbol == subSymbol);
                                if (idx2 == -1) throw new GMC4Exception("サブルーチン命令が存在しません。");
                                gmc4_m.MemSet(adr++, cmd.subCmds[idx2].code);
                            }
                            // オペランドが１つの場合
                            else if (cmd.argNum == 1)
                            {
                                if (term.Length == 3)
                                {
                                    char opr = term[2][0];
                                    gmc4_m.MemSet(adr++, opr);
                                }
                                else throw new GMC4Exception($"{cmd.symbol}のオペランドがありません。");
                            }
                            // オペランドが２つの場合
                            else if (cmd.argNum == 2)
                            {
                                if (term.Length == 3)
                                {
                                    string labelName = term[2];
                                    var labels = labelList.FindAll(n => n.name == labelName);
                                    if (labels.Count == 0) throw new GMC4Exception($"ラベル'{labelName}'が見つかりませんでした。");
                                    else if (labels.Count > 1) throw new GMC4Exception($"ラベル'{labelName}'が複数定義されています。");
                                    foreach (var c in labels[0].address) gmc4_m.MemSet(adr++, c);
                                }
                                else throw new GMC4Exception("ラベルがありません。");
                            }
                        }
                    }
                }
                for (byte i = 0; i < gmc4.Mem.Length; i++)
                {
                    try { gmc4.MemSet(i, gmc4_m.MemGet(i)); }
                    catch (GMC4Exception) { }
                }
                MemoryTextChange();
                LogLabelStatusStrip.Text = "アセンブル成功";
            }
            catch (GMC4Exception exp)
            {
                LogLabelStatusStrip.Text = "アセンブル失敗：" + exp.Message;
            }
            /*catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }*/
        }

        private void StepRun()
        {
            int idx = Array.FindIndex(cmds, c => CharToInt(c.code) == gmc4.MemGet(address));
            if (idx == -1) return;
            Command cmd = cmds[idx];
            switch (cmd.symbol)
            {
                case "KA": // KA
                    if (key != -1)
                    {
                        gmc4.AregSet(key);
                        exeFlag = false;
                    }
                    else
                        exeFlag = true;
                    address++;
                    break;
                case "AO": // AO
                    Set7SegLED(gmc4.AregGet());
                    exeFlag = true;
                    address++;
                    break;
                case "CH": // CH
                    gmc4.SwapReg(RegName.A_REG, RegName.B_REG);
                    gmc4.SwapReg(RegName.Y_REG, RegName.Z_REG);

                    exeFlag = true;
                    address++;
                    break;
                case "CY": // CY
                    exeFlag = gmc4.SwapReg(RegName.A_REG, RegName.Y_REG);
                    address++;
                    break;
                case "AM": // AM
                    exeFlag = gmc4.DataSet(gmc4.AregGet());
                    address++;
                    break;
                case "MA": // MA
                    exeFlag = gmc4.AregSet(gmc4.DataGet());
                    address++;
                    break;
                case "M+": // M+
                    {
                        int sum = gmc4.AregGet() + gmc4.DataGet();
                        gmc4.AregSet(sum);
                        exeFlag = sum > 0xF;
                        address++;
                        break;
                    }
                case "M-": // M-
                    {
                        int def = gmc4.DataGet() - gmc4.AregGet();
                        gmc4.AregSet(def);
                        exeFlag = def < 0;
                        address++;
                        break;
                    }
                case "TIA": // TIA
                    exeFlag = gmc4.AregSet(gmc4.MemGet(address + 1));
                    address += 2;
                    break;
                case "AIA": // AIA
                    {
                        int sum = gmc4.AregGet() + gmc4.MemGet(address + 1);
                        gmc4.AregSet(sum);
                        exeFlag = sum > 0xF;
                        address += 2;
                        break;
                    }
                case "TIY": // TIY
                    exeFlag = gmc4.YregSet(gmc4.MemGet(address + 1));
                    address += 2;
                    break;
                case "AIY": // AIY
                    {
                        int sum = gmc4.YregGet() + gmc4.MemGet(address + 1);
                        gmc4.YregSet(sum);
                        exeFlag = sum > 0xF;
                        address += 2;
                        break;
                    }
                case "CIA": // CIA
                    exeFlag = gmc4.AregGet() != gmc4.MemGet(address + 1);
                    address += 2;
                    break;
                case "CIY": // CIY
                    exeFlag = gmc4.YregGet() != gmc4.MemGet(address + 1);
                    address += 2;
                    break;
                case "JUMP":
                    if (exeFlag == true)
                    {
                        byte address_ = (byte)(gmc4.MemGet(address + 1) << 4 | gmc4.MemGet(address + 2));
                        address = address_;
                    }
                    else
                    {
                        exeFlag = true;
                        address += 3;
                    }
                    break;
                case "CAL":
                    if (exeFlag == true)
                    {
                        int idx2 = Array.FindIndex(cmd.subCmds, c => CharToInt(c.code) == gmc4.MemGet(address + 1));
                        if (idx2 == -1) return;

                        switch (cmd.subCmds[idx2].symbol)
                        {
                            case "RSTO":
                                Set7SegLED(-1);
                                break;
                            case "SETR": // CAL SETR
                                if (gmc4.YregGet() <= 6)
                                    binLEDs[gmc4.YregGet()].BackColor = Color.Red;
                                exeFlag = true;
                                break;
                            case "RSTR": // CAL RSTR
                                if (gmc4.YregGet() <= 6)
                                    binLEDs[gmc4.YregGet()].BackColor = Color.Black;
                                exeFlag = true;
                                break;
                            case "CMPL": // CAL CMPL
                                gmc4.AregSet(~gmc4.AregGet());
                                exeFlag = true;
                                break;
                            case "CHNG": // CAL CHNG
                                gmc4.SwapReg(RegName.A_REG, RegName.A_REGD);
                                gmc4.SwapReg(RegName.B_REG, RegName.B_REGD);
                                gmc4.SwapReg(RegName.Y_REG, RegName.Y_REGD);
                                gmc4.SwapReg(RegName.Z_REG, RegName.Z_REGD);
                                exeFlag = true;
                                break;
                            case "SIFT": // CAL SIFT
                                {
                                    bool flag = ((gmc4.AregGet() & 0x1) == 0);
                                    gmc4.AregSet(gmc4.AregGet() << 1);
                                    exeFlag = flag;
                                    break;
                                }
                            case "ENDS": // CAL ENDS
                                endB.PlaySync();
                                exeFlag = true;
                                break;
                            case "ERRS": // CAL ERRS
                                exeFlag = true;
                                errorB.PlaySync();
                                break;
                            case "SHTS": // CAL SHTS
                                shortB.PlaySync();
                                exeFlag = true;
                                break;
                            case "LONS": // CAL LONS
                                longB.PlaySync();
                                timer1.Interval = 1000;
                                exeFlag = true;
                                break;
                            case "SUND": // CAL SUND
                                Int4 tone = gmc4.AregGet();
                                if (tone >= 1 && tone <= 0xE) sund[tone - 1].PlaySync();
                                exeFlag = true;
                                break;
                            case "TIMR": // CAL TIMR
                                timer1.Interval = (gmc4.AregGet() + 1) * 100;
                                exeFlag = true;
                                break;
                            case "DSPR": // CAL DSPR
                                {
                                    int a = gmc4.MemGet(0x5F) << 4 | gmc4.MemGet(0x5E);
                                    SetBinaryLED(a);
                                    exeFlag = true;
                                    break;
                                }
                            case "DEM-": // CAL DEM-
                                {
                                    int a = gmc4.DataGet() - gmc4.AregGet();
                                    if (a < 0)
                                    {
                                        a += 10;
                                        gmc4.DataSet(gmc4.YregGet() - 1, 1);
                                    }
                                    gmc4.DataSet(a);
                                    gmc4.YregSet(gmc4.YregGet() - 1);
                                    exeFlag = true;
                                    break;
                                }
                            case "DEM+": // CAL DEM+
                                {
                                    int a = gmc4.DataGet() + gmc4.AregGet();
                                    gmc4.DataSet(a % 10);
                                    for (Int4 i = gmc4.YregGet() - 1; a > 9; i--)
                                    {
                                        a = gmc4.DataGet(i) + 1;
                                        gmc4.DataSet(i, a % 10);
                                    }
                                    gmc4.YregSet(gmc4.YregGet() - 1);
                                    exeFlag = true;
                                    break;
                                }
                        }
                    }
                    address += 2;
                    break;
            }
            if (address > 0x5F) address = 0;
            AddressChange();
            MemoryTextChange();
        }
        // タイマー実行時の処理（実行）
        private void Timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            int interval = int.Parse(SpeedToolStripTextBox.Text);
            timer1.Interval = interval > 0 ? interval : 1;
            StepRun();
            timer1.Start();
        }

        private int CharToInt(char a)
        {
            if (a >= '0' && a <= '9')
            {
                return a - '0';
            }
            else if (a >= 'A' && a <= 'F')
            {
                return a - 'A' + 10;
            }
            return -1;
        }
        private char IntToChar(int a)
        {
            if (a >= 0 && a <= 9)
            {
                return (char)(a + '0');
            }
            else if (a >= 0xA && a <= 0xF)
            {
                return (char)(a - 10 + 'A');
            }
            return (char)0;
        }

        private void SpeedResetToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SpeedToolStripTextBox.Text = 10.ToString();
        }

        private void SpeedToolStripTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            //0～9と、バックスペース以外の時は、イベントをキャンセルする
            if ((e.KeyChar < '0' || '9' < e.KeyChar) && e.KeyChar != '\b')
            {
                e.Handled = true;
            }
        }

        private void RunToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            RunToolStripMenuItem1.Enabled = false;
            StepRunToolStripMenuItem.Enabled = false;
            StopToolStripMenuItem.Enabled = true;
            Start();
        }

        private void StepRunToolStripMenuItem_Click(object sender, EventArgs e)
        {
            NextStepToolStripMenuItem.Enabled = true;

            RunToolStripMenuItem1.Enabled = false;
            StepRunToolStripMenuItem.Enabled = false;
            StopToolStripMenuItem.Enabled = true;
            Start(true);
        }

        private void NextStepToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StepRun();
        }

        private void StopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunToolStripMenuItem1.Enabled = true;
            StepRunToolStripMenuItem.Enabled = true;
            NextStepToolStripMenuItem.Enabled = false;
            StopToolStripMenuItem.Enabled = false;
            Stop();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) Start();
            if (keyData == Keys.F10) StepRun();

            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    struct Label
    {
        public string name;
        public string address;

        public Label(string str, int address)
        {
            name = str;
            this.address = address.ToString("X2");
        }
    }
    public class Command
    {
        public string symbol;
        public char code;
        public int argNum;
        public Command[] subCmds = new Command[0];

        public Command(string symbol, int code, int argNum = 0)
        {
            this.symbol = symbol;
            this.code = code.ToString("X1")[0];
            this.argNum = argNum;
        }
        public Command(string symbol, int code, Command[] subCmds)
        {
            this.symbol = symbol;
            this.code = code.ToString("X1")[0];
            this.argNum = 1;
            this.subCmds = subCmds;
        }
    }
}
