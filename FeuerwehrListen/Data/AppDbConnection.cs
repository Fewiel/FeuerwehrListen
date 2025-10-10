using LinqToDB;
using LinqToDB.Data;
using FeuerwehrListen.Models;

namespace FeuerwehrListen.Data;

public class AppDbConnection : DataConnection
{
    public AppDbConnection(DataOptions<AppDbConnection> options) : base(options.Options)
    {
    }

    public ITable<AttendanceList> AttendanceLists => this.GetTable<AttendanceList>();
    public ITable<OperationList> OperationLists => this.GetTable<OperationList>();
    public ITable<AttendanceEntry> AttendanceEntries => this.GetTable<AttendanceEntry>();
    public ITable<OperationEntry> OperationEntries => this.GetTable<OperationEntry>();
    public ITable<User> Users => this.GetTable<User>();
    public ITable<ApiKey> ApiKeys => this.GetTable<ApiKey>();
    public ITable<ScheduledList> ScheduledLists => this.GetTable<ScheduledList>();
    public ITable<Vehicle> Vehicles => this.GetTable<Vehicle>();
    public ITable<Member> Members => this.GetTable<Member>();
    public ITable<OperationFunctionDef> OperationFunctionDefs => this.GetTable<OperationFunctionDef>();
    public ITable<OperationEntryFunction> OperationEntryFunctions => this.GetTable<OperationEntryFunction>();
    public ITable<Keyword> Keywords => this.GetTable<Keyword>();
    public ITable<PersonalRequirement> PersonalRequirements => this.GetTable<PersonalRequirement>();
}

