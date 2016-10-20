using System;
using System.Collections;
using System.Threading;
using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Gadgeteer.Modules.GHIElectronics;
using GHI.Processor;
using Microsoft.SPOT.Hardware;
using GHI.Glide;
using System.Text;
using GHI.Glide.Geom;
using Json.NETMF;
using GHI.Glide.UI;
using GHI.SQLite;

namespace LoraDataReceiver
{
    public partial class Program
    {
        static int Counter = 0;
        //lora init
        private static SimpleSerial _loraSerial;
        private static string[] _dataInLora;
        //lora reset pin
        static OutputPort _restPort = new OutputPort(GHI.Pins.FEZSpiderII.Socket11.Pin3, true);
        private static string rx;
        //UI
        GHI.Glide.UI.TextBlock txtTime = null;
        GHI.Glide.UI.DataGrid GvData = null;
        GHI.Glide.UI.Button BtnReset = null;
        GHI.Glide.Display.Window window = null;
        GHI.Glide.UI.TextBlock txtMessage = null;
        //database
        Database myDatabase = null;
        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            //set display
            this.videoOut.SetDisplayConfiguration(VideoOut.Resolution.Vga800x600);
            //set glide
            window = GlideLoader.LoadWindow(Resources.GetString(Resources.StringResources.Form1));

            txtTime = (GHI.Glide.UI.TextBlock)window.GetChildByName("txtTime");
            GvData = (GHI.Glide.UI.DataGrid)window.GetChildByName("GvData");
            BtnReset = (GHI.Glide.UI.Button)window.GetChildByName("BtnReset");
            txtMessage = (GHI.Glide.UI.TextBlock)window.GetChildByName("TxtMessage");
            Glide.MainWindow = window;

            //setup grid
            //create grid column
            GvData.AddColumn(new DataGridColumn("Time", 200));
            GvData.AddColumn(new DataGridColumn("Temp", 200));
            GvData.AddColumn(new DataGridColumn("Humid", 200));
            GvData.AddColumn(new DataGridColumn("Light", 200));
            GvData.AddColumn(new DataGridColumn("Gas", 200));


            // Create a database in memory,
            // file system is possible however!
            myDatabase = new GHI.SQLite.Database();
            myDatabase.ExecuteNonQuery("CREATE Table Sensor" +
            " (Time TEXT, Temp DOUBLE,Humid DOUBLE,Light DOUBLE,Gas DOUBLE)");
            //reset database n display
            BtnReset.TapEvent += (object sender) =>
            {
                Counter = 0;
                myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                GvData.Clear();
                GvData.Invalidate();
            };

            //reset lora
            _restPort.Write(false);
            Thread.Sleep(1000);
            _restPort.Write(true);
            Thread.Sleep(1000);


            _loraSerial = new SimpleSerial(GHI.Pins.FEZSpiderII.Socket11.SerialPortName, 57600);
            _loraSerial.Open();
            _loraSerial.DataReceived += _loraSerial_DataReceived;
            //get version
            _loraSerial.WriteLine("sys get ver");
            Thread.Sleep(1000);
            //pause join
            _loraSerial.WriteLine("mac pause");
            Thread.Sleep(1500);
            //antena power
            _loraSerial.WriteLine("radio set pwr 14");
            Thread.Sleep(1500);
            //set device to receive
            _loraSerial.WriteLine("radio rx 0"); //set module to RX
            txtMessage.Text = "LORA-RN2483 setup has been completed...";
            txtMessage.Invalidate();
            window.Invalidate();
            //myDatabase.Dispose();

        }
        //convert hex to string
        string HexStringToString(string hexString)
        {
            if (hexString == null || (hexString.Length & 1) == 1)
            {
                throw new ArgumentException();
            }
            var sb = new StringBuilder();
            for (var i = 0; i < hexString.Length; i += 2)
            {
                var hexChar = hexString.Substring(i, 2);
                sb.Append((char)Convert.ToByte(hexChar));
            }
            return sb.ToString();
        }
        //convert hex to ascii
        private string HexString2Ascii(string hexString)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= hexString.Length - 2; i += 2)
            {
                int x = Int32.Parse(hexString.Substring(i, 2));
                sb.Append(new string(new char[] { (char)x }));
            }
            return sb.ToString();
        }
        //lora data received
        void _loraSerial_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            _dataInLora = _loraSerial.Deserialize();
            for (int index = 0; index < _dataInLora.Length; index++)
            {
                rx = _dataInLora[index];
                //if error
                if (_dataInLora[index].Length > 7)
                {
                    if (rx.Substring(0, 9) == "radio_err")
                    {
                        Debug.Print("!!!!!!!!!!!!! Radio Error !!!!!!!!!!!!!!");
                        PrintToLCD("Radio Error");

                        _restPort.Write(false);
                        Thread.Sleep(1000);
                        _restPort.Write(true);
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("mac pause");
                        Thread.Sleep(1000);
                        _loraSerial.WriteLine("radio rx 0");
                        return;

                    }
                    //if receive data
                    if (rx.Substring(0, 8) == "radio_rx")
                    {
                        string hex = _dataInLora[index].Substring(10);

                        Mainboard.SetDebugLED(true);
                        Thread.Sleep(500);
                        Mainboard.SetDebugLED(false);

                        Debug.Print(hex);
                        Debug.Print(Unpack(hex));
                        //update display

                        PrintToLCD(Unpack(hex));
                        Thread.Sleep(100);
                        // set module to RX
                        _loraSerial.WriteLine("radio rx 0");
                    }
                }
            }

        }
        //extract hex to string
        public static string Unpack(string input)
        {
            byte[] b = new byte[input.Length / 2];

            for (int i = 0; i < input.Length; i += 2)
            {
                b[i / 2] = (byte)((FromHex(input[i]) << 4) | FromHex(input[i + 1]));
            }
            return new string(Encoding.UTF8.GetChars(b));
        }
        public static int FromHex(char digit)
        {
            if ('0' <= digit && digit <= '9')
            {
                return (int)(digit - '0');
            }

            if ('a' <= digit && digit <= 'f')
                return (int)(digit - 'a' + 10);

            if ('A' <= digit && digit <= 'F')
                return (int)(digit - 'A' + 10);

            throw new ArgumentException("digit");
        }
        void PrintToLCD(string message)
        {
            String[] origin_names = null;
            ArrayList tabledata = null;
            //cek message
            if (message != null && message.Length > 0)
            {
                try
                {

                    if (message == "Radio Error") return;
                    var obj = Json.NETMF.JsonSerializer.DeserializeString(message) as Hashtable;
                    var detail = obj["Data"] as Hashtable;
                    DeviceData data = new DeviceData() { DeviceSN = obj["DeviceSN"].ToString() };
                    data.Data = new DataSensor() { Gas = Convert.ToDouble(detail["Gas"].ToString()), Temp = Convert.ToDouble(detail["Temp"].ToString()), Humid = Convert.ToDouble(detail["Humid"].ToString()), Light = Convert.ToDouble(detail["Light"].ToString()) };
                    //update display
                    txtTime.Text = DateTime.Now.ToString("dd/MMM/yyyy HH:mm:ss");
                    txtMessage.Text = "Data Reveiced Successfully.";
                    txtTime.Invalidate();
                    txtMessage.Invalidate();

                    var TimeStr = DateTime.Now.ToString("dd/MM/yy HH:mm");
                    //insert to db
                    var item = new DataGridItem(new object[] { TimeStr, data.Data.Temp, data.Data.Humid, data.Data.Light, data.Data.Gas });
                    //add data to grid
                    GvData.AddItem(item);
                    Counter++;

                    GvData.Invalidate();
                    window.Invalidate();

                    //add rows to table
                    myDatabase.ExecuteNonQuery("INSERT INTO Sensor (Time, Temp,Humid,Light,Gas)" +
                    " VALUES ('" + TimeStr + "' , " + data.Data.Temp + ", " + data.Data.Humid + ", " + data.Data.Light + ", " + data.Data.Gas + ")");
                    window.Invalidate();
                    if (Counter > 13)
                    {
                        //reset
                        Counter = 0;
                        myDatabase.ExecuteNonQuery("DELETE FROM Sensor");
                        GvData.Clear();
                        GvData.Invalidate();
                    }
                    /*
                    // Process SQL query and save returned records in SQLiteDataTable
                    ResultSet result = myDatabase.ExecuteQuery("SELECT * FROM Sensor");
                    // Get a copy of columns orign names example
                    origin_names = result.ColumnNames;
                    // Get a copy of table data example
                    tabledata = result.Data;
                    String fields = "Fields: ";
                    for (int i = 0; i < result.ColumnCount; i++)
                    {
                        fields += result.ColumnNames[i] + " |";
                    }
                    Debug.Print(fields);
                    object obj;
                    String row = "";
                    for (int j = 0; j < result.RowCount; j++)
                    {
                        row = j.ToString() + " ";
                        for (int i = 0; i < result.ColumnCount; i++)
                        {
                            obj = result[j, i];
                            if (obj == null)
                                row += "N/A";
                            else
                                row += obj.ToString();
                            row += " |";
                        }
                        Debug.Print(row);
                    }
                    */

                }
                catch (Exception ex)
                {
                    txtMessage.Text = message + "_" + ex.Message + "_" + ex.StackTrace;
                    txtMessage.Invalidate();
                }
            }

        }








    }

    public static class ByteExt
    {
        private static char[] _hexCharacterTable = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

#if MF_FRAMEWORK_VERSION_V4_1
    public static string ToHexString(byte[] array, string delimiter = "-")
#else
        public static string ToHexString(this byte[] array, string delimiter = "-")
#endif
        {
            if (array.Length > 0)
            {
                // it's faster to concatenate inside a char array than to
                // use string concatenation
                char[] delimeterArray = delimiter.ToCharArray();
                char[] chars = new char[array.Length * 2 + delimeterArray.Length * (array.Length - 1)];

                int j = 0;
                for (int i = 0; i < array.Length; i++)
                {
                    chars[j++] = (char)_hexCharacterTable[(array[i] & 0xF0) >> 4];
                    chars[j++] = (char)_hexCharacterTable[array[i] & 0x0F];

                    if (i != array.Length - 1)
                    {
                        foreach (char c in delimeterArray)
                        {
                            chars[j++] = c;
                        }

                    }
                }

                return new string(chars);
            }
            else
            {
                return string.Empty;
            }
        }
    }


    #region Model Classes
    public class DeviceData
    {
        public string DeviceSN { set; get; }
        public DataSensor Data { set; get; }
    }

    public class DataSensor
    {
        public double Gas { set; get; }
        public double Temp { set; get; }
        public double Humid { set; get; }
        public double Light { set; get; }

    }
    #endregion
}
