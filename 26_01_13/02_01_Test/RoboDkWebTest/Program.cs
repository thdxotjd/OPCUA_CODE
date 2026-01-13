using RoboDkWebTest.Services;

var builder = WebApplication.CreateBuilder(args);

// RoboDK 서비스 등록 (Singleton)
builder.Services.AddSingleton<RoboDkService>();

// Swagger 추가 (테스트 편의)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// ===== API 엔드포인트 =====

// 연결 테스트
app.MapGet("/api/robot/connect", async (RoboDkService roboDk) =>
{
    var result = await roboDk.ConnectAsync();
    return Results.Ok(new { success = result, message = result ? "연결 성공" : "연결 실패" });
})
.WithName("Connect")
.WithTags("RoboDK");

// 연결 해제
app.MapGet("/api/robot/disconnect", async (RoboDkService roboDk) =>
{
    await roboDk.DisconnectAsync();
    return Results.Ok(new { message = "연결 해제됨" });
})
.WithName("Disconnect")
.WithTags("RoboDK");

// 상태 확인
app.MapGet("/api/robot/status", (RoboDkService roboDk) =>
{
    return Results.Ok(new
    {
        isConnected = roboDk.IsConnected,
        endpointUrl = "opc.tcp://localhost:4840"
    });
})
.WithName("Status")
.WithTags("RoboDK");

// RoboDK 정보 읽기 (Variable 노드)
app.MapGet("/api/robot/info", async (RoboDkService roboDk) =>
{
    if (!roboDk.IsConnected)
        return Results.BadRequest(new { error = "먼저 /api/robot/connect 호출 필요" });

    var info = await roboDk.GetRoboDkInfoAsync();
    return Results.Ok(info);
})
.WithName("GetInfo")
.WithTags("RoboDK");

// Joint 값 읽기 (Method 호출)
app.MapGet("/api/robot/joints/{robotName}", async (string robotName, RoboDkService roboDk) =>
{
    if (!roboDk.IsConnected)
        return Results.BadRequest(new { error = "먼저 /api/robot/connect 호출 필요" });

    var joints = await roboDk.GetJointsAsync(robotName);
    if (joints == null)
        return Results.NotFound(new { error = $"로봇 '{robotName}' 찾을 수 없음" });

    return Results.Ok(new { robotName, joints });
})
.WithName("GetJoints")
.WithTags("RoboDK");

// Joint 값 설정 (Method 호출)
app.MapPost("/api/robot/joints/{robotName}", async (string robotName, JointRequest request, RoboDkService roboDk) =>
{
    if (!roboDk.IsConnected)
        return Results.BadRequest(new { error = "먼저 /api/robot/connect 호출 필요" });

    var success = await roboDk.SetJointsAsync(robotName, request.Joints);
    return success
        ? Results.Ok(new { message = "Joint 설정 성공", joints = request.Joints })
        : Results.BadRequest(new { error = "Joint 설정 실패" });
})
.WithName("SetJoints")
.WithTags("RoboDK");

app.Run();

// Request DTO
public record JointRequest(string Joints);
