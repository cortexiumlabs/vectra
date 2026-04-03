//using Vectra.Middleware;

//namespace Vectra.Endpoints;

//public class Proxy : EndpointGroupBase
//{
//    public override void Map(WebApplication app)
//    {
//        app.Map("/proxy/{**catch-all}", proxyApp =>
//        {
//            proxyApp.UseMiddleware<ProxyMiddleware>();
//        });
//    }
//}