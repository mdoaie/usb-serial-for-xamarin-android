/* Copyright 2011-2013 Google Inc.
 * Copyright 2013 mike wakerly <opensource@hoho.com>
 * Copyright 2015 Yasuyuki Hamada <yasuyuki_hamada@agri-info-design.com>
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301,
 * USA.
 *
 * Project home page: https://github.com/ysykhmd/usb-serial-for-xamarin-android
 * 
 * This project is based on usb-serial-for-android and ported for Xamarin.Android.
 * Original project home page: https://github.com/mik3y/usb-serial-for-android
 */

using System;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Widget;

using Aid.UsbSerial;
using System.Threading;

namespace UsbSerialExamples
{
    [Activity(Label = "@string/app_name")]

    public class SerialConsoleActivity : Activity
    {
        const string TAG = "SerialConsoleActivity";

        enum TEST_STATUS { STANDBY, TESTING }
        enum TEST_PERIOD : Int16{ SEC30 = 30, MIN01 = 60, MIN03 = 180, MIN05 = 300, MIN10 = 600 }

        const int DEFAULT_TRANSFAR_RATE = 19200;

        static UsbSerialPort mUsbSerialPort = null;

        TEST_STATUS ActivityStatus;
        ScrollView ScrollView;
        CheckData CheckInstance;

        String DeviceName;
        Boolean IsCdcDevice;
        Timer UpdateTestResultTimer;
        Timer TestMainTimer;
        Int32 TestTimePeriod = 10; // 5 * 60;
        Int32 TestTimeRemain;
        int TransfarRate;

        TextView TestModeTextView;
        TextView TitleTextView;
        TextView TransfarRateTitleTextView;
        TextView TransfarRateValueTextView;
        TextView ActivityStatusTextView;
        TextView TestTimeTextView;
        TextView RemainTimeTextView;
        TextView GoodCountTextView;
        TextView ErrorCountTextView;
        TextView TotalCountTextView;

        TextView DumpTextView;

        IMenuItem TestPeriodMenuItem;
        IMenuItem TransfarRateMenuItem;


        Button ModeChangeButton;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.serial_console);

            ActionBar.SetTitle(Resource.String.test_console_title);

            ActivityStatus = TEST_STATUS.STANDBY;

            TransfarRate = DEFAULT_TRANSFAR_RATE;

            DeviceName = mUsbSerialPort.GetType().Name;

            if (0 == String.Compare(DeviceName, 0, "Cdc", 0, 3))
            {
                IsCdcDevice = true;
            }
            else
            {
                IsCdcDevice = false;
            }

            TestModeTextView = (TextView)FindViewById(Resource.Id.test_mode);
            TitleTextView = (TextView)FindViewById(Resource.Id.serial_device_name);

            TransfarRateTitleTextView = (TextView)FindViewById(Resource.Id.title_transfar_rate);
            TransfarRateValueTextView = (TextView)FindViewById(Resource.Id.transfar_rate_value);
            if (IsCdcDevice)
            {
                TransfarRateTitleTextView.Enabled = false;
                TransfarRateValueTextView.Enabled = false;
            }
            else
            {
                TransfarRateValueTextView.SetText(TransfarRate.ToString(), TextView.BufferType.Normal);
            }

            DumpTextView = (TextView)FindViewById(Resource.Id.consoleText);
            ScrollView = (ScrollView)FindViewById(Resource.Id.demoScroller);

            ActivityStatusTextView = (TextView)FindViewById(Resource.Id.activity_status);
            ActivityStatusTextView.SetText(Resource.String.activity_status_standby);

            TestTimeTextView = (TextView)FindViewById(Resource.Id.test_time);
            TestTimeTextView.SetText(string.Format("{0:0#}:{1:0#}", TestTimePeriod / 60, TestTimePeriod % 60), TextView.BufferType.Normal);

            RemainTimeTextView = (TextView)FindViewById(Resource.Id.remain_time);
            RemainTimeTextView.SetText(Resource.String.remain_time_initial);

            GoodCountTextView = (TextView)FindViewById(Resource.Id.good_count);
            ErrorCountTextView = (TextView)FindViewById(Resource.Id.error_count);
            TotalCountTextView = (TextView)FindViewById(Resource.Id.total_count);

            ModeChangeButton = (Button)FindViewById(Resource.Id.modeChange);
            ModeChangeButton.Click += ModeChangeButtonHandler;

            //            CheckInstance = new CheckNmeaCheckSum();
            CheckInstance = new CheckCyclic00ToFF();

            TestModeTextView.SetText(CheckInstance.TestMode, TextView.BufferType.Normal);

            mUsbSerialPort.DataReceivedEventLinser += DataReceivedHandler;
        }

        public override Boolean OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.test_console_menu, menu);

            TestPeriodMenuItem = menu.FindItem(Resource.Id.menu_test_period);
            TransfarRateMenuItem = menu.FindItem(Resource.Id.menu_transfer_rate);
            if (IsCdcDevice)
            {
                TransfarRateMenuItem.SetEnabled(false);
            }


            return base.OnPrepareOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if (CheckTestPeriodMenu(item))
            {
                return true;
            }

            if (CheckTransferRateMenu(item))
            {
                return true;
            }

            if (CheckTestModeMenu(item))
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        bool CheckTestPeriodMenu(IMenuItem item)
        {
            TEST_PERIOD test_period;

            switch (item.ItemId)
            {
                case Resource.Id.test_period_30sec:
                    test_period = TEST_PERIOD.SEC30;
                    break;
                case Resource.Id.test_period_1min:
                    test_period = TEST_PERIOD.MIN01;
                    break;
                case Resource.Id.test_period_3min:
                    test_period = TEST_PERIOD.MIN03;
                    break;
                case Resource.Id.test_period_5min:
                    test_period = TEST_PERIOD.MIN05;
                    break;
                case Resource.Id.test_period_10min:
                    test_period = TEST_PERIOD.MIN10;
                    break;
                default:
                    return false;
            }
            TestTimePeriod = (Int16)test_period;
            RunOnUiThread(() =>
                TestTimeTextView.SetText(string.Format("{0:0#}:{1:0#}", TestTimePeriod / 60, TestTimePeriod % 60), TextView.BufferType.Normal)
            );
            return true;

        }

        bool CheckTransferRateMenu(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.transfer_rate_4800:
                    TransfarRate = 4800;
                    break;
                case Resource.Id.transfer_rate_9600:
                    TransfarRate = 9600;
                    break;
                case Resource.Id.transfer_rate_19200:
                    TransfarRate = 19200;
                    break;
                case Resource.Id.transfer_rate_38400:
                    TransfarRate = 38400;
                    break;
                case Resource.Id.transfer_rate_57600:
                    TransfarRate = 57600;
                    break;
                case Resource.Id.transfer_rate_115200:
                    TransfarRate = 115200;
                    break;
                default:
                    return false;
            }
            mUsbSerialPort.Baudrate = TransfarRate;
            mUsbSerialPort.ResetParameters();
            RunOnUiThread(() =>
                TransfarRateValueTextView.SetText(TransfarRate.ToString(), TextView.BufferType.Normal)
            );
            return true;
        }

        bool CheckTestModeMenu(IMenuItem item)
        {
            switch (item.ItemId)
            {
                case Resource.Id.test_mode_nmew_check_sum:
                    CheckInstance = new CheckNmeaCheckSum();
                    break;
                case Resource.Id.test_mode_cyclic_0x00_to_0xff:
                    CheckInstance = new CheckCyclic00ToFF();
                    break;
                default:
                    return false;
            }
            RunOnUiThread(() =>
                TestModeTextView.SetText(CheckInstance.TestMode, TextView.BufferType.Normal)
            );
            return true;

        }

        void ModeChangeButtonHandler(object sender, EventArgs e)
        {
            if (ActivityStatus == TEST_STATUS.STANDBY)
            {
                StartTest();
            }
            else
            {
                CancelTest();
            }
        }

        void StartTest()
        {
            CheckInstance.ResetProc();
            ActivityStatus = TEST_STATUS.TESTING;
            StartTestMainTimer();
            SetUpdateTestResultTimer();
            ActivityStatusTextView.SetText(Resource.String.activity_status_testing);
            ModeChangeButton.SetText(Resource.String.test_cancel);
            TestTimeRemain = TestTimePeriod;
            TestPeriodMenuItem.SetEnabled(false);
            TransfarRateMenuItem.SetEnabled(false);
        }

        void CancelTest()
        {
            ActivityStatus = TEST_STATUS.STANDBY;
            UpdateTestResultTimer.Dispose();
            TestMainTimer.Dispose();
            ActivityStatusTextView.SetText(Resource.String.activity_status_standby);
            ModeChangeButton.SetText(Resource.String.test_start);
            TestPeriodMenuItem.SetEnabled(true);
            if (!IsCdcDevice)
            {
                TransfarRateMenuItem.SetEnabled(true);
            }

        }

        void FinishTestHandler(Object sender)
        {
            Object thisLock = new Object();
            lock (thisLock)
            {
                ActivityStatus = TEST_STATUS.STANDBY;
                UpdateTestResultTimer.Dispose();
                TestMainTimer.Dispose();
                RunOnUiThread(() =>
                {
                    ActivityStatusTextView.SetText(Resource.String.activity_status_standby);
                    RemainTimeTextView.SetText(Resource.String.remain_time_normal_end);
                    ModeChangeButton.SetText(Resource.String.test_start);
                    TestPeriodMenuItem.SetEnabled(true);
                    if (!IsCdcDevice)
                    {
                        TransfarRateMenuItem.SetEnabled(true);
                    }
                });
            }
        }

        void StartTestMainTimer()
        {
            TestMainTimer = new Timer(FinishTestHandler, this, TestTimePeriod * 1000, Timeout.Infinite);
        }

        void SetUpdateTestResultTimer()
        {
            UpdateTestResultTimer = new Timer(UpdateTestResultDisplay, this, 0, 1000);
        }

        void UpdateTestResultDisplay(Object sender)
        {
            Object thisLock = new Object();
            lock(thisLock)
            {
                RunOnUiThread(() => {
                    RemainTimeTextView.SetText(string.Format("{0:0#}:{1:0#}", TestTimeRemain / 60, TestTimeRemain % 60), TextView.BufferType.Normal);
                    GoodCountTextView. SetText(CheckInstance.GoodCountString,  TextView.BufferType.Normal);
                    ErrorCountTextView.SetText(CheckInstance.ErrorCountString, TextView.BufferType.Normal);
                    TotalCountTextView.SetText(CheckInstance.TotalCountString, TextView.BufferType.Normal);
                });
                TestTimeRemain -= 1;
                if (TestTimeRemain < 0)
                {
                    TestTimeRemain = 0;
                }

            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (mUsbSerialPort != null)
            {
                try
                {
                    mUsbSerialPort.Close();
                    mUsbSerialPort.DataReceivedEventLinser -= DataReceivedHandler;
                }
                catch (Exception)
                {
                    // Ignore.
                }
            }
            Finish();
        }

        protected override void OnResume() {
			base.OnResume ();
			Log.Debug (TAG, "Resumed, port=" + mUsbSerialPort);
			if (mUsbSerialPort == null) {
				TitleTextView.Text = "No serial device.";
			} else {
				try {
                    mUsbSerialPort.Baudrate = TransfarRate;
					mUsbSerialPort.Open ();
                    TransfarRateValueTextView.SetText(TransfarRate.ToString(), TextView.BufferType.Normal);
                } catch (Exception e) {
					Log.Error (TAG, "Error setting up device: " + e.Message, e);
					TitleTextView.Text = "Error opening device: " + e.Message;
					try {
						mUsbSerialPort.Close ();
					} catch (Exception) {
						// Ignore.
					}
					mUsbSerialPort = null;
					return;
				}
				TitleTextView.Text = "Serial device: " + DeviceName;
			}
		}

        private void DataReceivedHandler(object sender, DataReceivedEventArgs e)
        {
            Object thisLock = new Object();
            lock (thisLock)
            {
                byte[] data = new byte[16384];
                int length = e.Port.Read(data, 0);
                RunOnUiThread(() => { UpdateReceivedData(data, length); });
                if (ActivityStatus == TEST_STATUS.TESTING)
                {
                    for (int i = 0; i < length; i++)
                    {
                        CheckInstance.ProcData(data[i]);
                    }
                }
            }
        }

        public void UpdateReceivedData(byte[] data, int length)
        {
//            string message = "Read " + length + " bytes: \n" + HexDump.DumpHexString(data, 0, length) + "\n\n";
//			DumpTextView.Append(message);
//			DumpTextView.Append(System.Text.Encoding.Default.GetString(data, 0, length));
//            ScrollView.SmoothScrollTo(0, DumpTextView.Bottom);
        }

        /**
         * Starts the activity, using the supplied driver instance.
         *
         * @param context
         * @param driver
         */
		static public void Show(Context context, UsbSerialPort port)
        {
            mUsbSerialPort = port;
            Intent intent = new Intent(context, typeof(SerialConsoleActivity));
            intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.NoHistory);
            context.StartActivity(intent);
        }

        void DisplayTransfarRateMenu(bool state)
        {
        }
    }

    abstract class CheckData
    {
        abstract public string TestMode { get; }
        abstract public string GoodCountString { get; }
        abstract public string ErrorCountString { get; }
        abstract public string TotalCountString { get; }
        abstract public void ProcData(byte data);
        abstract public void ResetProc();
    }

    class CheckNmeaCheckSum : CheckData
    {
        enum STATE : byte { IDLE, DATA, SUM1, SUM2 };

        public override string TestMode { get { return "NMEA Check Sum"; } }
        public override string GoodCountString  { get { return goodCount. ToString(); } }
        public override string ErrorCountString { get { return errorCount.ToString(); } }
        public override string TotalCountString { get { return totalCount.ToString(); } }
        STATE state = STATE.IDLE;
        Int64 goodCount = 0;
        Int64 errorCount = 0;
        Int64 totalCount = 0;
        byte calcSum = 0;
        byte getSum = 0;
        byte firstCharValue;
        byte secondCharValue;
        Object thisLock = new Object();

        public override void ResetProc()
        {
            goodCount = 0;
            errorCount = 0;
            totalCount = 0;
            state = STATE.IDLE;
        }

        public override void ProcData(byte data)
        {
            switch (state)
            {
                case STATE.IDLE:
                    if (data == '$')
                    {
                        state = STATE.DATA;
                        calcSum = 0;
                    }
                    break;
                case STATE.DATA:
                    if (data == '*')
                    {
                        state = STATE.SUM1;
                    }
                    else
                    {
                        calcSum ^= data;
                    }
                    break;
                case STATE.SUM1:
                    firstCharValue = CharToHex(data);
                    state = STATE.SUM2;
                    break;
                case STATE.SUM2:
                    secondCharValue = CharToHex(data);
                    getSum = (byte)(firstCharValue * 16 + secondCharValue);
                    state = STATE.IDLE;
                    lock(thisLock)
                    {
                        if (calcSum == getSum)
                        {
                            goodCount += 1;
                        }
                        else
                        {
                            errorCount += 1;
                        }
                        totalCount += 1;
                    }
                    break;
            }
        }

        byte CharToHex(byte c)
        {
            if (c >= '0' && c <= '9')
            {
                return (byte)(c - '0');
            }
            else
            {
                return (byte)((c & 0x0f) + 9);
            }
        }
    }


    class CheckCyclic00ToFF : CheckData
    {
        enum STATE : byte { IDLE, DATA };

        public override string TestMode { get { return "Cyclic 0x00 to 0xFF"; } }
        public override string GoodCountString { get { return goodCount.ToString(); } }
        public override string ErrorCountString { get { return errorCount.ToString(); } }
        public override string TotalCountString { get { return totalCount.ToString(); } }
        STATE state = STATE.IDLE;
        Int64 goodCount = 0;
        Int64 errorCount = 0;
        Int64 totalCount = 0;
        Object thisLock = new Object();

        static byte LastData;


        public override void ResetProc()
        {
            goodCount = 0;
            errorCount = 0;
            totalCount = 0;
            state = STATE.IDLE;
        }

        public override void ProcData(byte data)
        {
            switch (state)
            {
                case STATE.IDLE:
                    if (0x00 == data)
                    {
                        state = STATE.DATA;
                        LastData = data;
                    }
                    break;
                case STATE.DATA:
                    if (0x00 == data)
                    {
                        if (0xFF == LastData)
                        {
                            goodCount += 1;
                            LastData = data;
                        }
                        else
                        {
                            errorCount += 1;
                            LastData = data;
                        }

                    }
                    else
                    {
                        if (data - 1 == LastData)
                        {
                            LastData = data;
                        }
                        else
                        {
                            errorCount += 1;
                            state = STATE.IDLE;
                        }
                    }
                    break;
            }
            
        }
    }
}