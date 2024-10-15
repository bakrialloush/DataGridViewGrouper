using System;
using System.ComponentModel;

namespace Test
{
    public class TestData : INotifyPropertyChanged
    {
        string text;
        DateTime? date;
        int i;

        public event PropertyChangedEventHandler PropertyChanged;

        public string AString
        {
            get => text; set
            {
                text = value;
                NotifyChanged("AString");
            }
        }

        public DateTime? ADate
        {
            get => date;
            set
            {
                date = value;
                NotifyChanged("ADate");
            }
        }

        public int AnInt
        {
            get => i;
            set
            {
                i = value;
                NotifyChanged("AnInt");
            }
        }

        void NotifyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public static BindingList<TestData> CreateTestData()
        {
            var list = new BindingList<TestData>();

            var rnd = new Random();
            var dt = DateTime.Today;

            for (int i = 0; i < 100; i++)
            {
                list.Add(new TestData
                {
                    AString = new string((char)('A' + rnd.Next(0, 25)), 8),
                    ADate = dt.AddDays(rnd.Next(-100, 100)),
                    AnInt = rnd.Next(0, 50)
                });
            };
            return list;
        }
    }
}
