# ASP.NET CORE WEB API STUDY

## Program.cs
Program.cs ASP.NET Core Web API 프로그램의 시작점
여기서 하는 일은 크게 2가지

1. 서비스(Service) 등록하기
→ DI 컨테이너에 필요한 클래스들을 미리 올려두는 단계

2. 웹 서버(WebApplication) 구성 및 실행

## ASP.NET Core 서비스 등록 설명
1. Controller 기능을 DI 컨테이너에 등록
즉 "API 컨트롤러를 사용할 준비"를 하는 것.

AddControllers()는 ASP.NET Core가 컨트롤러를 자동으로 생성하고 실행할 수 있게 하는 기본 설정
→ 없으면 API 컨트롤러가 작동하지 않음.

2. 전역 필터( Global Filter ) 등록
```c#
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ApiLoggingActionFilter>();
});
```
→ 모든 컨트롤러의 액션이 실행될 때
자동으로 ApiLoggingActionFilter가 실행됨.

3. 서비스를 싱글톤(Singleton)으로 등록
인터페이스로 등록했기 때문에 컨트롤러에서는 ITagMappingService로 의존성을 받음
```c#
builder.Services.AddSingleton<ITagMappingService, TagMappingService>();
```

| 구성요소                       | 역할                                   |
| ------------------------------ | ---------------------------------------|
| **Controller**                 | API 요청 → Service 호출                |
| **Service (TagMapping/OpcUa)** | 비즈니스 로직 수행                     |
| **Filter**                     | 모든 API 호출 전/후 공통 처리 (로그 등)|
| **Program.cs**                 | 서비스 DI 등록 + 전체 앱 초기 설정     |


## Controller 구성

**어노테이션(Attributes)**
[ApiController]
[Route("api/[controller]")]

[ApiController]
자동 모델 바인딩 오류 400 처리, [FromBody] 추론, 바인딩/유효성 검사 관련 기본 동작을 제공.

[Route("api/[controller]")]
라우트 템플릿. 컨트롤러 클래스명이 OpcUaController면 기본 경로는 api/opcua가 됨. (대소문자 무관)

**HTTP 메서드**

✔️ [HttpGet]
- 데이터 조회(Read)
- Body가 없음
EX: 조회 API

✔️ [HttpPost]
- 데이터 생성(Create)
- Body(예: JSON) 포함 가능
EX: 로그인, 등록, 커넥션 요청 등

✔️ [HttpPut]
- 데이터 전체 업데이트(Update)
- 기존 데이터가 없으면 새로 만드는 경우도 있음
- POST와 비슷해 보이지만 의미적으로 “전체 교체”

✔️ [HttpPatch]
- 부분 업데이트(Partial Update)
- 특정 필드만 수정할 때 사용

✔️ [HttpDelete]
삭제 요청

커스텀 경로 지정
[HttpPost("connect")]
public IActionResult Connect() { ... }

파라미터 포함 경로 지정
[HttpGet("{id}")]
public IActionResult Get(int id) { ... }

**예제**
```c#
[HttpPost("connect")]
public async Task<ActionResult<OpcUaResponse<bool>>> Connect([FromBody] OpcUaConnectionRequest request)
```
[HttpPost("connect")]
 - HTTP POST, 경로 api/opcua/connect.

 Task<ActionResult<OpcUaResponse<bool>>>
 비동기 메서드. 성공 시 200 OK + OpcUaResponse<bool> 바디를 반환하고, 오류 시 400 BadRequest 반환.

 [FromBody] OpcUaConnectionRequest request
클라이언트가 보낸 JSON 바디를 OpcUaConnectionRequest로 바인딩. [ApiController]가 있으면 [FromBody]는 생략 가능하지만 명시해둔 상태.