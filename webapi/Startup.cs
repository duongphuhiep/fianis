using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Threading;
using Serilog;
using Serilog.Context;

namespace webapi
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseMvc();
            SetupWebSocket(app);
        }

        public void SetupWebSocket(IApplicationBuilder app)
        {
            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);

            app.Use(async (context, next) =>
            {
                using (LogContext.PushProperty("context", "middleware"))
                {
                    if (context.Request.Path == "/ws")
                    {
                        using (LogContext.PushProperty("context", "middleware.websocket"))
                        {
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                Log.Information("AcceptWebSocketAsync start");
                                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                                Log.Information("AcceptWebSocketAsync end");

                                await Echo(context, webSocket);
                            }
                            else
                            {
                                context.Response.StatusCode = 400;
                            }
                        }
                    }
                    else
                    {
                        await next();
                    }
                }
            });
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            using (LogContext.PushProperty("context", "EchoServer"))
            {
                Log.Information("Echo server start");

                var buffer = new byte[1024 * 4];
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    Log.Information("Send message {@count} bytes start", result.Count);
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                    Log.Information("Send message end");

                    Log.Information("Listen to message start");
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    Log.Information("Listen to message end");
                }
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                Log.Information("Echo server end");
            }
        }
    }
}
