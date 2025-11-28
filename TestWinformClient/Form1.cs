using OCST.FW.Common;
using OCST.FW.WinForm.Controls.DataGridView;
using System.Data;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text.Json;
using TestWinformClient.Biz;
using TestWinformClient.Model;

namespace TestWinformClient
{
    public partial class Form1 : Form
    {
        private HttpClient? _httpClient;
        private CancellationTokenSource? _streamCancellation;
        private string? _groupId;

        public Form1()
        {
            InitializeComponent();
            InitializeSession();
        }
        private void InitializeSession()
        {
            Session.Instance.DBConnectString = AppConfig.GetConfigurationManagerConnectionString("TESTDB");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            ConnectionOpcServer();
            InitializeGrid(gridOPCData);
        }

        private void InitializeGrid(DataGridView grid)
        {
            Column[] columns = new Column[]
            {
                new Column("No", "No", 40,true, DataGridViewContentAlignment.MiddleCenter),
                new Column("ClientHandle","Client Handle", 50,false, DataGridViewContentAlignment.MiddleCenter),
                new Column("AddressName", "Address", 200,true, DataGridViewContentAlignment.MiddleLeft),
                new Column("UpdateRate","UpdateRate",50,false, DataGridViewContentAlignment.MiddleCenter),
                new Column("Description", "Description", 200,true, DataGridViewContentAlignment.MiddleLeft),
                new Column("Value", "Value", 100, true,DataGridViewContentAlignment.MiddleCenter),
                new Column("TimeStamp", "TimeStamp", 200,true, DataGridViewContentAlignment.MiddleCenter),
                new Column("PLCCode","PLCCode",50,false, DataGridViewContentAlignment.MiddleCenter),
                new Column("GroupCode", "GroupCode", 80, false, DataGridViewContentAlignment.MiddleLeft)
            };

            GridHelper.InitDataGridView(grid, columns);

            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.Columns["TimeStamp"]?.DefaultCellStyle.Format = "yyyy-MM-dd HH:mm:ss";

            foreach (DataGridViewColumn col in grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        private async void ConnectionOpcServer()
        {
            try
            {
                _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5241") };

                // 1. OPC 서버 연결
                var connectResponse = await _httpClient.PostAsJsonAsync("/api/OpcUa/connect", new { });
                var connectResult = await connectResponse.Content.ReadFromJsonAsync<ApiResponse<bool>>();

                if (connectResult?.Success == true)
                {
                    lblStatus.Text = "Success";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "failure";
                MessageBox.Show($"연결 실패: {ex.Message}");
            }
        }

        private async Task GetTagListAsync(string groupCode)
        {
            try
            {
                DataSet ds = await TagMappingList_Nx.Instance.SelectTagData(groupCode);
                if (ds != null && ds.Tables.Count > 0)
                {
                    gridOPCData.DataSource = ds.Tables[0];
                }

                // API에 모니터링 시작 요청 (서버에서 DB 조회 수행)
                var monitorResponse = await _httpClient!.PostAsJsonAsync(
                    "/api/OpcUa/monitor/start",
                    new { groupCode });

                var monitorResult = await monitorResponse.Content
                    .ReadFromJsonAsync<ApiResponse<bool>>();

                if (monitorResult?.Success == true)
                {
                    // SSE 스트림 구독 시작
                    _groupId = groupCode;
                    _streamCancellation = new CancellationTokenSource();
                    _ = Task.Run(() => SubscribeToTagChanges(groupCode, _streamCancellation.Token));
                }
                else
                {
                    // 에러 메시지 표시
                    var errorMsg = !string.IsNullOrEmpty(monitorResult?.ErrorMessage)
                        ? monitorResult.ErrorMessage
                        : "모니터링 시작에 실패했습니다.";
                    MessageBox.Show($"모니터링 시작 실패: {errorMsg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"모니터링 시작 실패: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task SubscribeToTagChanges(string groupId, CancellationToken cancellationToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/OpcUa/monitor/stream/{groupId}");
                using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
                {
                    if (line.StartsWith("data: "))
                    {
                        var json = line.Substring(6); // "data: " 제거
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        var tagEvent = JsonSerializer.Deserialize<TagValueChangedEvent>(json, options);
                        if (tagEvent != null)
                        {
                            // UI 스레드에서 처리
                            if (InvokeRequired)
                            {
                                Invoke(new Action(() => HandleTagValueChanged(tagEvent)));
                            }
                            else
                            {
                                HandleTagValueChanged(tagEvent);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 종료
            }
            catch (Exception ex)
            {
                // 에러 처리
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                        MessageBox.Show($"스트림 오류: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)));
                }
            }
        }

        private void HandleTagValueChanged(TagValueChangedEvent tagEvent)
        {
            try
            {
                // ClientHandle을 int로 변환
                if (!int.TryParse(tagEvent.ClientHandle, out int clientHandle)) return;

                object itemValue = tagEvent.Value ?? new object();

                DataTable? dt = gridOPCData.DataSource as DataTable;
                if (dt != null)
                {
                    var row = dt.AsEnumerable().FirstOrDefault(r => Convert.ToInt32(r["ClientHandle"]) == clientHandle);

                    dt.Columns["Value"]?.ReadOnly = false;
                    dt.Columns["TimeStamp"]?.ReadOnly = false;

                    if (row == null) return;

                    if (string.IsNullOrEmpty(row["Value"].ToString()))
                    {
                        row["Value"] = itemValue;
                    }
                    else if (!string.Equals(row["Value"].ToString(), itemValue?.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        row["Value"] = itemValue;
                        row["TimeStamp"] = DateTime.Now;
                    }
                    else return;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            await GetTagListAsync("G0001");
        }
    }
}
