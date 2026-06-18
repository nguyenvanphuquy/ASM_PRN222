namespace DataAccessLayer.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Lecturer = "Lecturer";
    public const string Student = "Student";

    public static readonly string[] All = { Admin, Lecturer, Student };
}
