namespace FeuerwehrListen.Models;

public enum ListStatus
{
    Open = 1,
    Closed = 2
}

public enum OperationFunction
{
    Maschinist = 1,
    Gruppenfuehrer = 2,
    Trupp = 3
}

public enum ScheduledListType
{
    Attendance = 1,
    Operation = 2
}

public enum VehicleType
{
    LF = 1,
    TLF = 2,
    DLK = 3,
    RW = 4,
    MTW = 5,
    KdoW = 6,
    Sonstige = 99
}

public enum UserRole
{
    User = 1,
    Admin = 2
}

