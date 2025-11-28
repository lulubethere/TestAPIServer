using OCST.FW.Common;

namespace TestWinformClient
{
    internal class Session : SingleBase<Session>
    {
        public string DBConnectString { get; set; } = string.Empty;
    }
}
