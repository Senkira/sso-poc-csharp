# SSO Gemini Login POC — .NET 10

ตัวอย่างนี้แปลง flow จาก `sso-background-login-demo` เป็น C#/.NET 10 โดยคงลำดับการทำงานเดิม 8 ขั้นตอน และแยกส่วนรับผิดชอบออกจากกัน:

- `SsoGeminiLogin.Mvc` — Web SSO, UI, realtime pipeline และ activity log
- `SsoGeminiLogin.Api` — account mapping, opaque browser session และ Agent gateway
- `SsoGeminiLogin.Agent` — isolated Edge profile, Google login และ browser handoff

Mock database อยู่หลัง `IBrowserAccountMappingRepository` จึงเปลี่ยนเป็น database adapter จริงภายหลังได้โดยไม่ย้าย API เข้า MVC

## วิธีรัน

ไฟล์ portable ZIP สุดท้ายรวม .NET 10 runtime ไว้แล้ว เครื่องทดสอบจึงไม่ต้องติดตั้ง .NET SDK ให้แตก ZIP แล้วเรียกไฟล์เดียวที่ root:

```cmd
START-POC.cmd
```

Runner จะเลือก portable zero-install อัตโนมัติ, start Agent/API/MVC, ตรวจ health ของทุกส่วน แล้วเปิด <http://127.0.0.1:4173/> อัตโนมัติ หากเป็น source repository ที่ไม่มีโฟลเดอร์ `portable` runner จะใช้ .NET SDK `10.0.400` เพื่อ build แทน

ข้อมูลทดสอบ Web SSO:

```text
Username: ssotest01
Password: 123456
```

Google password ไม่อยู่ใน source, config หรือ log แต่ถูกอ่านภายใน Agent จาก Windows Credential Manager เท่านั้น

## Flow เดิม 8 ขั้นตอน

1. `SSO Verified` — `POST /sso/login`
2. `Identity Map` — `GET /api/v1/me`
3. `Broker Session` — `POST /api/v1/browser-sessions`
4. `Agent IPC` — `start(accountId)`
5. `Edge Profile` — `launchPersistentContext()`
6. `Google Login` — email → password → session
7. `Browser Handoff` — `POST /{sessionId}/open`
8. `Success` — `status: handed-off`

หลัง Web SSO สำเร็จ ระบบจะรอให้ผู้ใช้กด `เปิด Gemini Chat` ตามข้อกำหนดของ POC จากนั้นจึงทำขั้นตอน 3–8 อัตโนมัติ และเปิด Gemini ใน Microsoft Edge ด้วย isolated profile เดิม

## Runtime files

- Log: `.runtime\logs\api.log`, `.runtime\logs\mvc.log`
- Isolated Edge profile: `.runtime\profiles\`
- Local cookie protection keys: `.runtime\keys\`
- Source ไม่มี Google password
- ไม่มี release/ZIP ใน repository; จะสร้าง ZIP เพียงครั้งเดียวเมื่อได้รับคำสั่งส่งมอบ
