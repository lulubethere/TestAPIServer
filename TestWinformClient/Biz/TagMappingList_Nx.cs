using Dapper;
using OCST.FW.Common;
using OCST.FW.DBHelper;
using System.Data;

namespace TestWinformClient.Biz
{
    internal class TagMappingList_Nx : SingleBase<TagMappingList_Nx>
    {
        #region [ 태그 데이터 가져오기 (SelectTagData)]
        public async Task<DataSet> SelectTagData(string GroupCode)
        {
            try
            {
                string strProcedureName = "P_TagMappingList";
                DynamicParameters dynamicParameters = new DynamicParameters();
                dynamicParameters.Add("@IN_WORK_TYPE", "SelectTagData");
                dynamicParameters.Add("@IN_GroupCode", GroupCode);

                var dsTagList = await MssqlDpHelper.ExecuteReaderToDataSetAsync(CommandType.StoredProcedure, Session.Instance.DBConnectString, strProcedureName, dynamicParameters: dynamicParameters);
                return dsTagList;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
        #endregion
    }
}
