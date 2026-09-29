using ClassLibrary;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text;
using WpfEMCTestSoftware.Enums;
using WpfEMCTestSoftware.Models;
using WpfEMCTestSoftware.Models.DeviceModels;
using WpfEMCTestSoftware.RunTasks;

namespace WpfEMCTestSoftware
{
    public class ClsData
    {
        // 锁对象
        private static readonly object lockObject = new object();
        // 私有静态实例变量
        private static ClsData? instance;
        // 公共的静态属性，用于获取实例
        public static ClsData Instance
        {
            get
            {
                lock (lockObject)
                {
                    instance ??= new ClsData();
                    return instance;
                }
            }
        }

        private OleDbConnection Conn_QR = null;
        private ClsData()
        {
            OpenConnect("EMC_DB", "Sinpel2025");
            UpdateUserActive();
        }

        /// <summary>
        /// 接续测试临时曲线数据行（按 模板名 + 通道Key + 曲线标题 + 序号 唯一定位一条曲线点）。
        /// 用于 Req 2：中途停止后把已测幅值/功率写入临时表，下次可选择从该频点续测。
        /// </summary>
        public class CurveTempRow
        {
            public string ChannelKey { get; set; } = "";
            public string LineTitle { get; set; } = "";
            public int Seq { get; set; }
            public double XHz { get; set; }
            public double YValue { get; set; }
            public bool IsReference { get; set; }
        }

        /// <summary>
        /// 开启连接
        /// </summary>
        private bool OpenConnect(string BaseName, string pwd)
        {
            string basePath = $"{AppDomain.CurrentDomain.BaseDirectory}{BaseName}.accdb";

            string strConn = $"Provider = Microsoft.ACE.OLEDB.12.0; Data Source = '{basePath}';" +
                 $"User ID = admin; Jet OLEDB:Database Password = {pwd};";

            Conn_QR = new(strConn);

            if (File.Exists(basePath))
            {
                if (AccessHelper.CreateMDBDataBase(strConn))
                {
                    // 创建Access表 para
                    string cmdText = @"CREATE TABLE para (ID AUTOINCREMENT, paraname TEXT(20),
                paravalue TEXT(255),opttime DateTime, CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表 operlog
                    cmdText = @"CREATE TABLE operlog (ID AUTOINCREMENT, operdata TEXT(100),
                opervalue LONGTEXT,createtime DateTime, CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表 TestRecord
                    cmdText = @"CREATE TABLE TestRecord (ID AUTOINCREMENT,TestName TEXT(20),
                FrequencyBand TEXT(20),ReportID TEXT(50),TestType TEXT(20),TestDate TEXT(10),Chart1 TEXT(20),
                Chart2 TEXT(20),Chart3 TEXT(20),createtime DateTime,CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    //创建Access表 ChartData
                    cmdText = @"CREATE TABLE ChartData (ID AUTOINCREMENT,ChartName TEXT(20), 
                XValue double,YValue double,createtime DateTime,CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表 DeviceTable
                    cmdText = @"CREATE TABLE DeviceTable (ID AUTOINCREMENT,Name TEXT(50),DeviceType TEXT(50),  
                DeviceModel int,Communicationmode int,IP TEXT(20),Port int,ComPort TEXT(10),ComBaud TEXT(10),CANChannel int,
                CANBaudRate int,Address TEXT(50),IsActive YESNO,CommunicationTime int,OtherPara LONGTEXT,
                createtime DateTime,CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表 ReverbUniformityRecord
                    cmdText = @"CREATE TABLE ReverbUniformityRecord (ID AUTOINCREMENT,[TestName] TEXT(50),[TestDate] TEXT(50),[TestStandard] int,[TestMode] int,[Position] int,[Frequency] double,[Angle] double,[XFiled] double,[YFiled] double,[ZField] double,[CField] double,[ForwardPower] double,[ReversePower] double,[RecvPower] double,[createtime] DateTime,CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表临时表 ReverbUniformityTemp
                    cmdText = @"CREATE TABLE ReverbUniformityTemp (ID AUTOINCREMENT,[TestName] TEXT(50),[TestDate] TEXT(50),[TestStandard] int,[TestMode] int,[Position] int,[Frequency] double,[Angle] double,[XFiled] double,[YFiled] double,[ZField] double,[CField] double,[ForwardPower] double,[ReversePower] double,[RecvPower] double,[createtime] DateTime,CONSTRAINT Record_PK PRIMARY KEY(ID));";
                    AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    // 创建Access表 接续测试临时曲线表 CurveTempData（按模板名隔离，支持所有测试项通用续测）
                    if (!AccessHelper.TableIsExists(Conn_QR, "CurveTempData"))
                    {
                        cmdText = @"CREATE TABLE CurveTempData (ID AUTOINCREMENT,TemplateName TEXT(50),ChannelKey TEXT(50),LineTitle TEXT(50),Seq INT,XHz DOUBLE,YValue DOUBLE,IsRef YESNO,createtime DateTime,CONSTRAINT PK_CurveTemp PRIMARY KEY(ID));";
                        AccessHelper.ExecuteAccess(Conn_QR, cmdText);
                    }
                    //创建Access表 UserInfo
                    SaveUserInfo(new ClsUserInfo()
                    {
                        IsActive = true,
                        Level = 0,
                        PassWord = "admin",
                        UserId = "admin",
                        UserName = "admin"
                    });
                    LogManager.WriteLog($"生成Access数据库{BaseName}成功！");
                }
                else
                {
                    LogManager.WriteLog($"生成Access数据库{BaseName}失败！");
                    return false;
                }
            }
            ClearRecord();
            return true;
        }



        #region 用户操作
        /// <summary>
        /// 用户是否存在
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public bool IsExistsUser(string userID)
        {
            string sql = $"select count(1) from UserInfo where [UserName]='{userID}';";
            string? obj = AccessHelper.GetTopData(Conn_QR, sql);
            return !string.IsNullOrEmpty(obj) && Convert.ToInt32(obj) > 0;
        }

        /// <summary>
        /// 获取用户等级
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public int GainUserLevel(string userID)
        {
            string sql = $"select UserLevel from UserInfo where [UserName]='{userID}';";
            string? obj = AccessHelper.GetTopData(Conn_QR, sql);
            return string.IsNullOrEmpty(obj) ? 0 : Convert.ToInt32(obj);
        }

        /// <summary>
        /// 保存用户信息
        /// </summary>
        public bool SaveUserInfo(ClsUserInfo userInfo)
        {
            StringBuilder sql = new();
            if (IsExistsUser(userInfo.UserName))
            {
                sql.Append($"update UserInfo set [Password]='{userInfo.PassWord}',");
                sql.Append($"UserLevel={userInfo.Level},VidPeriod={userInfo.ValidityPeriod},");
                sql.Append($"IsActive={(userInfo.IsActive ? 1 : 0)} where [UserName]='{userInfo.UserName}'");
            }
            else
            {
                sql.Append(@"insert into UserInfo(UserId,[UserName],[Password],UserLevel,VidPeriod,IsActive,createtime)values(");
                sql.Append($"'{userInfo.UserId}','{userInfo.UserName}','{userInfo.PassWord}',");
                sql.Append($"{userInfo.Level},{userInfo.ValidityPeriod},");
                sql.Append($"{(userInfo.IsActive ? 1 : 0)},Now())");
            }
            return AccessHelper.ExecuteAccess(Conn_QR, sql.ToString()) >= 1;
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="newPwd"></param>
        public bool UpdateUserPwd(string userID, string newPwd)
        {
            string sql = $"update UserInfo set [Password]='{newPwd}' where [UserName]='{userID}'";
            return AccessHelper.ExecuteAccess(Conn_QR, sql) > 0;
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userID"></param>
        public bool DeleteUser(string userID)
        {
            string sql = $"delete from UserInfo where [UserName]='{userID}'";
            return AccessHelper.ExecuteAccess(Conn_QR, sql) > 0;
        }

        /// <summary>
        /// 更新用户激活状态
        /// </summary>
        public void UpdateUserActive()
        {
            string sql = $"update UserInfo set IsActive = 0 where DateDiff('d',createtime,Now()) > VidPeriod" +
                " and VidPeriod <> 0";
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }

        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="userID"></param>
        /// <returns></returns>
        public List<ClsUserInfo> GainUserInfo(string userID = "")
        {
            string sql = $"select * from UserInfo";
            if (userID != "")
                sql = string.Concat(sql, $" where [UserName] = '{userID}'");
            DataTable dataTable = AccessHelper.DataReader(Conn_QR, sql);
            return DataTableToUserInfo(dataTable);
        }

        /// <summary>
        /// DataTable转用户信息列表
        /// </summary>
        /// <param name="dataTable"></param>
        /// <returns></returns>
        private List<ClsUserInfo> DataTableToUserInfo(DataTable dataTable)
        {
            List<ClsUserInfo> userInfos = [];
            if (dataTable.Rows.Count > 0)
            {
                for (int i = 0; i < dataTable.Rows.Count; i++)
                {
                    DataRow dr = dataTable.Rows[i];
                    ClsUserInfo user = new()
                    {
                        CreationTime = dr["createtime"].ToString() ?? "",
                        UserId = dr["UserId"].ToString() ?? "",
                        UserName = dr["UserName"].ToString() ?? "",
                        PassWord = dr["Password"].ToString() ?? "",
                        Level = Convert.ToInt32(dr["UserLevel"]),
                        ValidityPeriod = Convert.ToInt32(dr["VidPeriod"]),
                        IsActive = Convert.ToBoolean(dr["IsActive"])
                    };
                    userInfos.Add(user);
                }
            }
            return userInfos;
        }

        #endregion

        #region 设备操作
        /// <summary>
        /// 判断设备是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsExistsDevice(string name)
        {
            string sql = $"select count(1) from DeviceTable where Name='{name}';";
            string? obj = AccessHelper.GetTopData(Conn_QR, sql);
            return string.IsNullOrEmpty(obj) ? false : Convert.ToInt32(obj) > 0;
        }

        public void SaveDevice(DeviceInfo device)
        {
            string json = device.Type switch
            {
                DeviceType.信号源 => DeviceSerAndDeser.DeviceParaSer(device as SignalSourcePara ?? new SignalSourcePara()),
                DeviceType.示波器 => DeviceSerAndDeser.DeviceParaSer(device as OscilloscopePara ?? new OscilloscopePara()),
                DeviceType.衰减器 => DeviceSerAndDeser.DeviceParaSer(device as PhaseAttPara ?? new PhaseAttPara()),
                DeviceType.功放 => DeviceSerAndDeser.DeviceParaSer(device as AmpifierPara ?? new AmpifierPara()),
                DeviceType.接收机 => DeviceSerAndDeser.DeviceParaSer(device as ReceiverPara ?? new ReceiverPara()),
                DeviceType.场强仪 => DeviceSerAndDeser.DeviceParaSer(device as FieldStrengthMeterPara ?? new FieldStrengthMeterPara()),
                DeviceType.功率计 => DeviceSerAndDeser.DeviceParaSer(device as PowerMeterPara ?? new PowerMeterPara()),
                DeviceType.倍频器 => DeviceSerAndDeser.DeviceParaSer(device as MultiplierFreqPara ?? new MultiplierFreqPara()),
                DeviceType.开关矩阵 => DeviceSerAndDeser.DeviceParaSer(device as SwitchMatrixPara ?? new SwitchMatrixPara()),
                DeviceType.频谱仪 => DeviceSerAndDeser.DeviceParaSer(device as SpectrumAnalyzerPara ?? new SpectrumAnalyzerPara()),
                DeviceType.天线 => DeviceSerAndDeser.DeviceParaSer(device as AntennaPara ?? new AntennaPara()),
                DeviceType.天线塔 => DeviceSerAndDeser.DeviceParaSer(device as AntennaTowerPara ?? new AntennaTowerPara()),
                DeviceType.搅拌器 => DeviceSerAndDeser.DeviceParaSer(device as StirrerPara ?? new StirrerPara()),
                DeviceType.矢量网络分析仪 => DeviceSerAndDeser.DeviceParaSer(device as DeviceInfo ?? new DeviceInfo())
            };
            SaveDeviceInfo(device, json);
        }

        /// <summary>
        /// 保存设备信息
        /// </summary>
        /// <param name="deviceInfo"></param>
        /// <param name="Other"></param>
        public void SaveDeviceInfo(DeviceInfo deviceInfo, string Other)
        {
            StringBuilder sql = new();
            if (IsExistsDevice(deviceInfo.Name))
            {
                sql.Append($"update DeviceTable set DeviceType='{deviceInfo.Type}',");
                sql.Append($"DeviceModel={(int)deviceInfo.Model},Communicationmode={(int)deviceInfo.Communicationmode},");
                sql.Append($"IP='{deviceInfo.DevicePara.IP}',Port={deviceInfo.DevicePara.Port},");
                sql.Append($"ComPort='{deviceInfo.DevicePara.ComPort}',ComBaud='{deviceInfo.DevicePara.ComBaud}',");
                sql.Append($"CANChannel={deviceInfo.DevicePara.CANChannel},CANBaudRate={(int)deviceInfo.DevicePara.CANBaudRate},");
                sql.Append($"Address='{deviceInfo.DevicePara.Address}',IsActive={(deviceInfo.IsActive ? 1 : 0)},");
                sql.Append($"CommunicationTime={deviceInfo.Time},OtherPara='{Other}' where ");
                sql.Append($"Name='{deviceInfo.Name}'");
            }
            else
            {
                sql.Append(@"insert into DeviceTable(Name,DeviceType,DeviceModel,Communicationmode,
IP,Port,ComPort,ComBaud,CANChannel,CANBaudRate,Address,IsActive,CommunicationTime,OtherPara,createtime)values(");
                sql.Append($"'{deviceInfo.Name}','{deviceInfo.Type}',{(int)deviceInfo.Model},");
                sql.Append($"{(int)deviceInfo.Communicationmode},'{deviceInfo.DevicePara.IP}',{deviceInfo.DevicePara.Port},");
                sql.Append($"'{deviceInfo.DevicePara.ComPort}','{deviceInfo.DevicePara.ComBaud}',");
                sql.Append($"{deviceInfo.DevicePara.CANChannel},{(int)deviceInfo.DevicePara.CANBaudRate},");
                sql.Append($"'{deviceInfo.DevicePara.Address}',{(deviceInfo.IsActive ? 1 : 0)},");
                sql.Append($"{deviceInfo.Time},'{Other}',Now())");
            }
            AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
        }

        /// <summary>
        /// 获取设备信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DataTable GainDeviceInfo(string Name = "")
        {
            string sql = $"select * from DeviceTable";
            if (Name != "")
                sql = string.Concat(sql, $" where Name = '{Name}'");
            DataTable dataTable = AccessHelper.DataReader(Conn_QR, sql);
            return dataTable;
        }

        /// <summary>
        /// 删除指定设备
        /// </summary>
        /// <param name="id"></param>
        public void DelDevice(string Name)
        {
            string sql = $"delete from DeviceTable where Name = '{Name}'";
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }
        #endregion


        #region 测试记录
        /// <summary>
        /// 保存图表记录信息
        /// </summary>
        /// <param name="name"></param>
        /// <param name="lstXValue"></param>
        /// <param name="lstYValue"></param>
        public void SaveChartRecord(string name, List<double> lstXValue, List<double> lstYValue)
        {
            if (!AccessHelper.TableIsExists(Conn_QR, name))
            {
                //不存在则创建图表存储表
                string cmdText = $"CREATE TABLE {name} (ID AUTOINCREMENT, XValue double,YValue double,opttime DateTime, CONSTRAINT Record_PK PRIMARY KEY(ID));";
                AccessHelper.ExecuteAccess(Conn_QR, cmdText);
            }
            int max = Math.Max(lstXValue.Count, lstYValue.Count);
            for (int i = 0; i < max; i++)
            {
                double xvalue, yvalue;
                xvalue = lstXValue.Count > i ? lstXValue[i] : 0;
                yvalue = lstYValue.Count > i ? lstYValue[i] : 0;
                string sql = $"insert into {name}(XValue,YValue,opttime)values({xvalue},{yvalue},now())";
                AccessHelper.ExecuteAccess(Conn_QR, sql);
            }
        }

        /// <summary>
        /// 获取图表记录
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public DataTable GainChartRecord(string name)
        {
            string sql = $"select XValue,YValue from {name}";
            DataTable dt = AccessHelper.DataReader(Conn_QR, sql);
            return dt;
        }

        /// <summary>
        /// 保存测试记录
        /// </summary>
        /// <param name="model"></param>
        public void SaveTestRecord(TestRecordModel model)
        {
            StringBuilder sql = new("insert into TestRecord (TestName,FrequencyBand,ReportID,TestType,TestDate,Chart,Chart2,Chart3,createtime)values(");
            sql.Append($"'{model.TestName}','{model.FrequencyBand}','{model.ReportID}','{model.TestType}'");
            sql.Append($"'{model.TestDate}','{model.ChartName1}','{model.ChartName2}','{model.ChartName3}',now())");
            AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
        }

        /// <summary>
        /// 获取测试记录
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public DataTable GainTestRecord(string name)
        {
            string sql = $"select * from TestRecord where TestName='{name}'";
            DataTable dt = AccessHelper.DataReader(Conn_QR, sql);
            return dt;
        }

        /// <summary>
        /// 获取所有测试记录的名称
        /// </summary>
        /// <returns></returns>
        public List<string?> GainTestRecord()
        {
            string sql = $"select TestName from TestRecord";
            DataTable dt = AccessHelper.DataReader(Conn_QR, sql);
            List<string?> list = dt.AsEnumerable().Select(d => d.Field<string>("TestName")).ToList();
            return list;
        }
        #endregion

        #region 参数配置
        /// <summary>
        /// 查询参数
        /// </summary>
        /// <param name="name"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        public string QueryPara(string name, string defaultValue = "")
        {
            string sql = $"select paravalue from para where paraname='{name}';";
            string? value = AccessHelper.GetTopData(Conn_QR, sql);
            if (string.IsNullOrEmpty(value))
            {
                AddOrUpdatePara(name, defaultValue);
                return defaultValue;
            }
            return value;
        }

        /// <summary>
        /// 参数是否存在
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int IsParaExists(string name)
        {
            string sql = $"select count(1) from para where paraname='{name}';";
            string? obj = AccessHelper.GetTopData(Conn_QR, sql);
            return string.IsNullOrEmpty(obj) ? 0 : Convert.ToInt32(obj);
        }

        /// <summary>
        /// 新增或修改参数
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void AddOrUpdatePara(string name, string value)
        {
            string sql;
            if (IsParaExists(name) == 0)
            {
                sql = $"INSERT INTO para (paraname, paravalue,opttime)VALUES ('{name}', '{value}',now());";
            }
            else
            {
                sql = $"update para set paravalue = '{value}',opttime = now() where paraname ='{name}';";
            }
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }
        #endregion

        /// <summary>
        /// 新增操作日志
        /// </summary>
        /// <param name="Data"></param>
        /// <param name="value"></param>
        public void AddOperLog(string Data, string value)
        {
            string sql = $"INSERT INTO operlog (operdata, opervalue,createtime) VALUES ('{Data}', '{value}',now());";
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }

        /// <summary>
        /// 获取操作日志
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public DataTable GetOperLog(DateTime? start = null, DateTime? end = null)
        {
            StringBuilder sql = new($"select * from operlog");
            if (start != null && end != null)
                sql.Append($" where createtime between '{start}' and '{end}'");
            sql.Append(';');
            DataTable dt = AccessHelper.DataReader(Conn_QR, sql.ToString());
            return dt;
        }

        /// <summary>
        /// 清理30/7天前的记录
        /// </summary>
        private void ClearRecord()
        {
            string sql = $"delete from operlog where Abs(DateDiff('d', createtime, Now())) > 90;";
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }

        public void Close()
        {
            Conn_QR?.Close();
        }


        internal void RenameDevice(string oldName, string newName)
        {
            if (IsExistsDevice(oldName))
            {
                string sql = $"UPDATE DeviceTable SET Name = '{newName}' WHERE Name = '{oldName}'";
                int result = AccessHelper.ExecuteAccess(Conn_QR, sql);
            }
        }

        internal void ModifyDevicesActive(ObservableCollection<DeviceInfo>? deviceInfos)
        {
            //设备列表"保存"按钮：把每行"状态"勾选写入数据库（此前为空方法，勾选永不落库）
            if (deviceInfos == null) return;
            foreach (DeviceInfo device in deviceInfos)
            {
                string sql = $"UPDATE DeviceTable SET IsActive = {(device.IsActive ? "True" : "False")} WHERE Name = '{device.Name}'";
                AccessHelper.ExecuteAccess(Conn_QR, sql);
            }
        }
        //==========================场均校准参数======================================//
        /// <summary>
        /// 位置点复测，替换数据根据（位置点，角度，频率，测试名称四个条件）
        /// </summary>
        /// <param name="context"></param>
        public void SaveTestContext(TestRunContext context)
        {
            StringBuilder sql = new StringBuilder();
            //优先UPDATE
            sql.Append("UPDATE ReverbUniformityTemp SET ");
            sql.Append("TestDate = '").Append(context.TestDate).Append("',");
            sql.Append("TestStandard = ").Append((int)context.curStandard).Append(",");
            sql.Append("TestMode = ").Append((int)context.testMode).Append(",");
            sql.Append("XFiled = ").Append(context.Ex).Append(",");
            sql.Append("YFiled = ").Append(context.Ey).Append(",");
            sql.Append("ZFiled = ").Append(context.Ez).Append(",");
            sql.Append("CFiled = ").Append(context.Ec).Append(",");
            sql.Append("ForwardPower = ").Append(context.Pinput).Append(",");
            sql.Append("ReversePower = 0 ");
            //四大条件
            sql.Append(" WHERE TestName = '").Append(context.TestName).Append("' ");
            sql.Append(" AND Position = ").Append(context.CurPosition);
            sql.Append(" AND Frequency = ").Append(context.CurrentFre);
            sql.Append(" AND Angle = ").Append(context.CurrentStirrerH);
            int affectRows = AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
            if (affectRows == 0)
            {
                sql.Clear();
                sql.Append("insert into ReverbUniformityTemp(");
                sql.Append("TestName,TestDate,TestStandard,TestMode,Position,Frequency,Angle,XFiled,YFiled,ZFiled,CFiled,ForwardPower,ReversePower,createtime");
                sql.Append(") values(");
                sql.Append($"'{context.TestName}',");
                sql.Append($"'{context.TestDate}',");
                sql.Append($"{(int)context.curStandard},");
                sql.Append($"{(int)context.testMode},");
                sql.Append($"{context.CurPosition},");
                sql.Append($"{context.CurrentFre},");
                sql.Append($"{context.CurrentStirrerH},");
                sql.Append($"{context.Ex},");
                sql.Append($"{context.Ey},");
                sql.Append($"{context.Ez},");
                sql.Append($"{context.Ec},");
                sql.Append($"{context.Pinput},");
                sql.Append("0,");
                sql.Append("Now()"); // Access 修正：Now()
                sql.Append(");");
                AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
            }
        }
        /// <summary>
        /// 根据标准查询，返回该标准下存在临时数据的测试名称（只取第一条）
        /// </summary>
        /// <param name="standard">测试标准枚举数值</param>
        /// <returns>存在数据返回TestName，无数据返回null</returns>
        public string GetUnfinishedTestName(int standard)
        {
            StringBuilder sql = new StringBuilder();
            // Access TOP 1 取第一条，DISTINCT去重防止同名多条点位
            sql.Append("SELECT TOP 1 DISTINCT TestName FROM ReverbUniformityTemp ");
            sql.Append("WHERE TestStandard = ").Append(standard);

            object result = AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());

            if (result == null || result == DBNull.Value)
                return null;

            return result.ToString().Trim();
        }

        /// <summary>
        /// 获取临时表里任意一条历史数据：测试标准、测试名称
        /// </summary>
        /// <returns>无数据返回null，有数据返回实体</returns>
        public TempHistoryInfo? GetAnyHistoryTempInfo()
        {
            StringBuilder sql = new StringBuilder();
            // Access TOP 1 随便取一条，只要表里有数据就能拿到标准和名称
            sql.Append("SELECT TOP 1 TestStandard,TestName FROM ReverbUniformityTemp");
            object resultObj = AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());

            if (resultObj is not DataTable dt || dt.Rows.Count == 0)
                return null;

            DataRow row = dt.Rows[0];
            int std = Convert.ToInt32(row["TestStandard"]);
            string testName = row["TestName"]?.ToString()?.Trim();

            return new TempHistoryInfo()
            {
                HistoryStandard = std,
                HistoryTestName = testName
            };
        }
        /// <summary>
        /// 清空临时表清空指定名称
        /// </summary>
        /// <param name="testName"></param>
        public void ClearOldTemplate(string testName)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("DELETE FROM ReverbUniformityTemp ");
            sql.Append("WHERE TestName = '").Append(testName.Replace("'", "''")).Append("' ");
            AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
        }
        /// <summary>
        /// 清空临时表所有数据
        /// </summary>
        public void ClearOldTemplate()
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("DELETE FROM ReverbUniformityTemp");
            //Access 删除全部临时点位
            AccessHelper.ExecuteAccess(Conn_QR, sql.ToString());
        }
        #region 接续测试临时曲线（Req 2：中途停止后可续测，通用所有测试项）
        /// <summary>清空指定模板的接续临时曲线</summary>
        public void ClearCurveTemp(string templateName)
        {
            if (string.IsNullOrEmpty(templateName)) return;
            string sql = $"DELETE FROM CurveTempData WHERE TemplateName='{templateName.Replace("'", "''")}'";
            AccessHelper.ExecuteAccess(Conn_QR, sql);
        }

        /// <summary>指定模板是否存在接续临时曲线</summary>
        public bool HasCurveTemp(string templateName)
        {
            if (string.IsNullOrEmpty(templateName)) return false;
            string sql = $"select count(1) from CurveTempData where TemplateName='{templateName.Replace("'", "''")}'";
            string? obj = AccessHelper.GetTopData(Conn_QR, sql);
            return !string.IsNullOrEmpty(obj) && Convert.ToInt32(obj) > 0;
        }

        /// <summary>把接续临时曲线整表写入（先清旧快照，再写最新进度，保证每次停止都是完整曲线）</summary>
        public void SaveCurveTempData(string templateName, List<CurveTempRow> rows)
        {
            if (string.IsNullOrEmpty(templateName) || rows == null || rows.Count == 0) return;
            ClearCurveTemp(templateName);
            foreach (var r in rows)
            {
                string sql = $"insert into CurveTempData(TemplateName,ChannelKey,LineTitle,Seq,XHz,YValue,IsRef,createtime)values(" +
                    $"'{templateName.Replace("'", "''")}','{r.ChannelKey.Replace("'", "''")}','{(r.LineTitle ?? "").Replace("'", "''")}'," +
                    $"{r.Seq},{r.XHz},{r.YValue},{(r.IsReference ? 1 : 0)},Now())";
                AccessHelper.ExecuteAccess(Conn_QR, sql);
            }
        }

        /// <summary>读取指定模板的接续临时曲线</summary>
        public List<CurveTempRow> GainCurveTempData(string templateName)
        {
            var list = new List<CurveTempRow>();
            if (string.IsNullOrEmpty(templateName)) return list;
            string sql = $"select ChannelKey,LineTitle,Seq,XHz,YValue,IsRef from CurveTempData where TemplateName='{templateName.Replace("'", "''")}' order by Seq";
            DataTable dt = AccessHelper.DataReader(Conn_QR, sql);
            if (dt != null)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(new CurveTempRow
                    {
                        ChannelKey = dr["ChannelKey"]?.ToString() ?? "",
                        LineTitle = dr["LineTitle"]?.ToString() ?? "",
                        Seq = Convert.ToInt32(dr["Seq"]),
                        XHz = Convert.ToDouble(dr["XHz"]),
                        YValue = Convert.ToDouble(dr["YValue"]),
                        IsReference = Convert.ToBoolean(dr["IsRef"]),
                    });
                }
            }
            return list;
        }
        #endregion

        /// <summary>
        /// 查询返回DataTable
        /// </summary>
        /// <param name="testName"></param>
        /// <returns></returns>
        public DataTable GetDataTableFromReverbUniformityTemp(string testName)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT Position,Frequency,Angle FROM ReverbUniformityTemp ");
            sql.Append("WHERE TestName = '").Append(testName.Replace("'", "''")).Append("'");
            return AccessHelper.DataReader(Conn_QR, sql.ToString());
        }

        public DataTable GetDataTableFromReverbUniformityRecord(string testName)
        {
            StringBuilder sql = new StringBuilder();
            // SELECT * 查询该testName下所有字段全部数据
            sql.Append("SELECT *  FROM ReverbUniformityRecord ");
            sql.Append("WHERE TestName = '").Append(testName.Replace("'", "''")).Append("'");

            return AccessHelper.DataReader(Conn_QR, sql.ToString());
        }

        public ObservableCollection<string> GetUniformityTestNames()
        {
            //  cmdText = @"CREATE TABLE ReverbUniformityRecord (ID AUTOINCREMENT,[TestName] TEXT(50),[TestDate] TEXT(50)
            ObservableCollection<string> result = new ObservableCollection<string>();
            string cmdText = @"SELECT DISTINCT TestName 
                       FROM ReverbUniformityRecord 
                       WHERE TestName IS NOT NULL AND TestName <> ''";
            DataTable dt = AccessHelper.DataReader(Conn_QR, cmdText);
            if (dt != null && dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string testName = row["TestName"]?.ToString().Trim();
                    if (!string.IsNullOrEmpty(testName))
                    {
                        result.Add(testName);
                    }
                }
            }
            return result;

        }
    }
}

