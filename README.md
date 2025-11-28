# Postman 테스트 가이드

## 기본 URL
- HTTP: `http://localhost:5241`
- HTTPS: `https://localhost:7103` (인증서 경고 무시 가능)

---

## 1. OPC UA 서버 연결

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/connect`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "endpointUrl": "",
  "useSecurity": false,
  "username": null,
  "password": null
}
```

또는 `appsettings.json`의 기본값을 사용하려면:
```json
{
  "endpointUrl": "",
  "useSecurity": false
}
```

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 2. 연결 상태 확인

**Method:** `GET`  
**URL:** `http://localhost:5241/api/OpcUa/status`

**Headers:** 없음

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 3. 노드 값 읽기

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/read`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "nodeId": "Melsec_QT.VD70T.M0100"
}
```

**예상 응답:**
```json
{
  "success": true,
  "data": 123.45,
  "errorMessage": null
}
```

---

## 4. 여러 노드 값 읽기

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/read-multiple`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "nodeIds": [
    "Melsec_QT.VD70T.M0100",
    "Melsec_QT.VD70T.M0101",
    "Melsec_QT.VD70T.M0102"
  ]
}
```

**예상 응답:**
```json
{
  "success": true,
  "data": {
    "Melsec_QT.VD70T.M0100": 123.45,
    "Melsec_QT.VD70T.M0101": 67.89,
    "Melsec_QT.VD70T.M0102": 10.0
  },
  "errorMessage": null
}
```

---

## 5. 노드 값 쓰기

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/write`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "nodeId": "Melsec_QT.VD70T.M0100",
  "value": 100,
  "dataType": "int"
}
```

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 6. 태그 그룹 모니터링 시작

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/monitor/start`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "groupCode": "G0001"
}
```

**설명:**
- 서버에서 DB를 조회하여 해당 그룹 코드의 태그 목록을 가져옵니다
- 각 태그에 대해 OPC UA 모니터링을 시작합니다
- 태그 값이 변경되면 SSE 스트림으로 전송됩니다

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 7. 태그 그룹 모니터링 중지

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/monitor/stop`

**Headers:**
```
Content-Type: application/json
```

**Body (JSON):**
```json
{
  "groupId": "G0001"
}
```

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 8. 태그 값 변경 이벤트 스트리밍 (SSE)

**Method:** `GET`  
**URL:** `http://localhost:5241/api/OpcUa/monitor/stream/{groupCode}`

**예시:**
```
http://localhost:5241/api/OpcUa/monitor/stream/G0001
```

**Headers:** 없음

**설명:**
- Server-Sent Events (SSE) 형식으로 태그 값 변경 이벤트를 실시간으로 수신합니다
- 모니터링이 시작된 후에만 이벤트를 받을 수 있습니다
- Postman에서는 "Send and Download" 버튼을 사용하거나, 별도의 SSE 클라이언트를 사용해야 합니다

**응답 형식 (SSE):**
```
data: {"nodeId":"130","value":123.45,"timestamp":"2024-01-01T12:00:00Z"}

data: {"nodeId":"131","value":67.89,"timestamp":"2024-01-01T12:00:01Z"}

```

**참고:**
- Postman에서 SSE를 테스트하려면 "Send and Download" 옵션을 사용하거나
- 브라우저에서 직접 접근하거나
- curl 명령어 사용: `curl -N http://localhost:5241/api/OpcUa/monitor/stream/G0001`

---

## 9. 연결 해제

**Method:** `POST`  
**URL:** `http://localhost:5241/api/OpcUa/disconnect`

**Headers:** 없음  
**Body:** 없음

**예상 응답:**
```json
{
  "success": true,
  "data": true,
  "errorMessage": null
}
```

---

## 테스트 순서

### 기본 테스트
1. **연결** → `POST /api/OpcUa/connect`
2. **상태 확인** → `GET /api/OpcUa/status`
3. **값 읽기** → `POST /api/OpcUa/read`
4. **값 쓰기** → `POST /api/OpcUa/write`
5. **연결 해제** → `POST /api/OpcUa/disconnect`

### 모니터링 테스트
1. **연결** → `POST /api/OpcUa/connect`
2. **모니터링 시작** → `POST /api/OpcUa/monitor/start` (groupCode: "G0001")
3. **이벤트 스트리밍** → `GET /api/OpcUa/monitor/stream/G0001` (별도 탭/클라이언트)
4. **모니터링 중지** → `POST /api/OpcUa/monitor/stop` (groupId: "G0001")
5. **연결 해제** → `POST /api/OpcUa/disconnect`

---

## 참고사항

### NodeId 형식
- KepServer에서는 TagName을 직접 사용합니다
- 예: `"Melsec_QT.VD70T.M0100"`
- 서버에서 자동으로 namespace index 2로 변환합니다

### DataType 옵션 (Write 시)
- `"int"` 또는 `"int32"`
- `"double"` 또는 `"float"`
- `"bool"` 또는 `"boolean"`
- `"string"`

### 모니터링 동작 방식
1. 클라이언트가 `POST /api/OpcUa/monitor/start`로 모니터링 시작 요청
2. 서버가 DB에서 그룹 코드에 해당하는 태그 목록 조회
3. 서버가 각 태그에 대해 OPC UA 모니터링 시작
4. 태그 값이 변경되면 SSE 스트림으로 클라이언트에 전송
5. 클라이언트는 `GET /api/OpcUa/monitor/stream/{groupCode}`로 이벤트 수신

### SSE 스트림 테스트 방법
- **Postman**: "Send and Download" 버튼 사용
- **브라우저**: 직접 URL 접근 (EventSource API 사용)
- **curl**: `curl -N http://localhost:5241/api/OpcUa/monitor/stream/G0001`
- **PowerShell**: 
  ```powershell
  $response = Invoke-WebRequest -Uri "http://localhost:5241/api/OpcUa/monitor/stream/G0001" -Method Get
  $response.Content
  ```
