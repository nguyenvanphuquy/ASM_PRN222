# -*- coding: utf-8 -*-
"""
Kiểm chứng REALTIME (SignalR) end-to-end:
1. Mở 1 SignalR client (đóng vai 'sinh viên đang xem trang Subjects').
2. Một admin tạo/xoá môn học qua REST API.
3. Xác nhận client nhận được sự kiện 'SubjectsChanged' NGAY LẬP TỨC,
   tức trình duyệt sẽ tự cập nhật mà KHÔNG cần reload.
"""
import asyncio, json, re, urllib.request, urllib.parse, http.cookiejar
import websockets

BASE = "http://localhost:5300"
WS   = "ws://localhost:5300"
RS   = "\x1e"  # record separator của SignalR

def make_session():
    jar = http.cookiejar.CookieJar()
    op = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
    html = op.open(BASE + "/Auth/Login").read().decode("utf-8", "replace")
    tok = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)
    op.open(urllib.request.Request(BASE + "/Auth/Login",
        data=urllib.parse.urlencode(
            {"Username": "admin", "Password": "admin123", "__RequestVerificationToken": tok}).encode()))
    cookie = "; ".join(f"{c.name}={c.value}" for c in jar)
    return op, cookie

def api(op, method, path, body=None):
    data = json.dumps(body).encode() if body is not None else None
    h = {"Content-Type": "application/json"} if data else {}
    r = urllib.request.Request(BASE + path, data=data, headers=h, method=method)
    try:
        resp = op.open(r); return resp.getcode(), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode("utf-8", "replace")

async def main():
    op, cookie = make_session()

    # negotiate để lấy connectionToken
    code, text = api(op, "POST", "/hubs/subjects/negotiate?negotiateVersion=1", body={})
    token = json.loads(text)["connectionToken"]
    print(f"[1] negotiate OK, connectionToken length={len(token)}")

    url = f"{WS}/hubs/subjects?id={urllib.parse.quote(token)}"
    async with websockets.connect(url, additional_headers={"Cookie": cookie}) as ws:
        # handshake
        await ws.send('{"protocol":"json","version":1}' + RS)
        ack = await ws.recv()
        print(f"[2] WebSocket handshake OK: {ack.strip(RS)!r}")
        print("[3] Client (sinh viên) đang LẮNG NGHE realtime…")

        got = asyncio.Event()
        received = {}

        async def listen():
            async for raw in ws:
                for part in raw.split(RS):
                    if not part:
                        continue
                    msg = json.loads(part)
                    if msg.get("type") == 1 and msg.get("target") == "SubjectsChanged":
                        received["payload"] = msg["arguments"][0]
                        got.set()
                        return
        listener = asyncio.create_task(listen())

        # Admin tạo môn học mới qua REST (mô phỏng thao tác ở máy khác)
        await asyncio.sleep(0.3)
        code, text = api(op, "POST", "/api/subjects",
                         body={"code": "RT999", "name": "Realtime Demo", "description": "đẩy realtime"})
        new_id = json.loads(text)["id"]
        print(f"[4] Admin POST /api/subjects -> HTTP {code} (tạo môn RT999)")

        # Chờ client nhận push
        try:
            await asyncio.wait_for(got.wait(), timeout=5)
            p = received["payload"]
            print(f"[5] ✅ CLIENT NHẬN PUSH realtime: action={p['action']} "
                  f"subject={p['subject']['code']} — KHÔNG cần reload trang!")
            ok1 = p["action"] == "created" and p["subject"]["code"] == "RT999"
        except asyncio.TimeoutError:
            print("[5] ❌ Không nhận được push trong 5s")
            ok1 = False
            listener.cancel()

        # Dọn dẹp: xoá môn demo (cũng phát realtime 'deleted')
        code, _ = api(op, "DELETE", f"/api/subjects/{new_id}")
        print(f"[6] Dọn dẹp: DELETE RT999 -> HTTP {code}")

        print("\n" + ("✅ REALTIME HOẠT ĐỘNG END-TO-END" if ok1 else "❌ REALTIME FAIL"))
        return 0 if ok1 else 1

if __name__ == "__main__":
    import sys
    sys.exit(asyncio.run(main()))
