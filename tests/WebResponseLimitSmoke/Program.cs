using System.Text;
using XPScript.Web.Runtime;

var response = new XpsWebResponse(8);
if (response.MaxBodyBytes != 8) throw new Exception("Configured response limit was not exposed.");

response.Write("1234");
response.WriteBinary(Encoding.UTF8.GetBytes("5678"));
if (Encoding.UTF8.GetString(response.Body.Span) != "12345678")
    throw new Exception("Response content changed at the exact configured limit.");

AssertLimit(() => response.Write("9"), "text append");
AssertLimit(() => response.WriteBinary([9]), "binary append");
if (Encoding.UTF8.GetString(response.Body.Span) != "12345678")
    throw new Exception("Rejected response write changed the existing body.");

response.Clear();
if (response.Body.Length != 0) throw new Exception("Clear did not reset response body usage.");
response.Write("12345678");

var fileResponse = new XpsWebResponse(4);
fileResponse.SendFile(Encoding.UTF8.GetBytes("1234"), "data.bin");
if (fileResponse.Body.Length != 4) throw new Exception("SendFile failed at the exact configured limit.");

var rejectedFile = new XpsWebResponse(4);
AssertLimit(() => rejectedFile.SendFile(Encoding.UTF8.GetBytes("12345"), "data.bin"), "SendFile");
if (rejectedFile.Body.Length != 0) throw new Exception("Rejected SendFile allocated response body bytes.");

if (new XpsWebResponse().MaxBodyBytes != XpsWebResponse.DefaultMaxBodyBytes)
    throw new Exception("Default response body limit was not applied.");

try
{
    _ = new XpsWebResponse(0);
    throw new Exception("Zero response limit was accepted.");
}
catch (ArgumentOutOfRangeException)
{
}

Console.WriteLine("WEB-RESPONSE-LIMIT=OK");

static void AssertLimit(Action action, string scenario)
{
    try
    {
        action();
        throw new Exception($"Response limit did not reject {scenario}.");
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("Response body exceeds", StringComparison.Ordinal))
    {
    }
}
