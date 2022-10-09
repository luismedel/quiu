using System.Net;
using quiu.http;

namespace quiu.tests;

public class AdminServerTests
    : ServerTestsBase<QuiuAdminServer>
{
    protected override string ServerHost => $"http://localhost:{App.Config["admin_server_port"]}";

    protected override QuiuAdminServer InitServer ()
    {
        App.Config["admin_server_host"] = "*";
        App.Config["admin_server_port"] = QuiuAdminServer.DEFAULT_PORT;

        return new QuiuAdminServer (App);
    }
}