namespace ManagedReferenceFixture;

public static class FixtureApi
{
    public static string Marker => "MANAGED-REFERENCE-FIXTURE";

    public static string DescribeObject(object? value) => value switch
    {
        null => "CLR-NULL",
        DBNull => "DBNULL",
        _ => value.GetType().FullName ?? value.GetType().Name
    };
}
