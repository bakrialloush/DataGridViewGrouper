using DevDash.Controls;
using System.Drawing;
using System.Windows.Forms;

namespace Test
{
    public partial class ExampleWithControl : Form
    {
        private readonly DevDash.Controls.DataGridViewGrouper _grouper;

        public ExampleWithControl()
        {
            InitializeComponent();
            dataGridView1.DataSource = TestData.CreateTestData();

            _grouper = new DevDash.Controls.DataGridViewGrouper(dataGridView1);
            _grouper.SetGroupOn("AString");
            _grouper.DisplayGroup += Grouper_DisplayGroup;

            //also valid:
            //grouper.SetGroupOn<TestData>(t => t.AString);
            //grouper.SetGroupOn(this.dataGridView1.Columns["AString"]);

            //to collapse all loaded rows: (the difference with setting the option above, is that after choosing a new grouping (or on a reload), the new groups would expand.
            _grouper.ExpandAll();
            _grouper.CollapseAll();

            //besides grouping on a property/column value, you can set a custom group:
            //grouper.SetCustomGroup<TestData>(t => t.AnInt % 10, "Mod 10");

            //to customize the grouping display, you can attach to the DisplayGroup event:
            //grouper.DisplayGroup += grouper_DisplayGroup;
        }

        //optionally, you can customize the grouping display by subscribing to the DisplayGroup event
        void Grouper_DisplayGroup(object sender, GroupDisplayEventArgs e)
        {
            // e.BackColor = (e.Group.GroupIndex % 2) == 0 ? Color.Orange : Color.LightBlue;
            e.BackColor = Color.LightSteelBlue;
            //e.Header = "[" + e.Header + "], grp: " + e.Group.GroupIndex;
            //e.DisplayValue = "Value is " + e.DisplayValue;
            //e.Summary = "contains " + e.Group.Count + " rows";
            e.Header = "تجريب نص طويل لتحديد عرض العنوان في هذا المكان";
        }
    }
}
