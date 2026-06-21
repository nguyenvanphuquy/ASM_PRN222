# -*- coding: utf-8 -*-
"""Smoke test cho REST API Subjects + Swagger + auth (Milestone 3)."""
import sys, json, re, urllib.request, urllib.parse, http.cookiejar

BASE = "http://localhost:5300"
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))

# Opener KHÔNG tự đi theo redirect — để quan sát đúng mã 302 khi chưa đăng nhập.
class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *a, **k):
        return None
opener_nr = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), _NoRedirect)

def req(method, path, data=None, json_body=None, want=None, follow=True):
    headers = {"Accept": "application/json"}
    body = None
    if json_body is not None:
        body = json.dumps(json_body).encode()
        headers["Content-Type"] = "application/json"
    elif data is not None:
        body = urllib.parse.urlencode(data).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    r = urllib.request.Request(BASE + path, data=body, headers=headers, method=method)
    op = opener if follow else opener_nr
    try:
        resp = op.open(r)
        code, text = resp.getcode(), resp.read().decode("utf-8", "replace")
    except urllib.error.HTTPError as e:
        code, text = e.code, e.read().decode("utf-8", "replace")
    ok = "OK " if (want is None or code == want) else "FAIL"
    print(f"[{ok}] {method:6} {path:28} -> HTTP {code}" + (f" (mong {want})" if want and code != want else ""))
    return code, text

def main():
    passed = True

    # 0. Chưa đăng nhập -> phải bị chặn (302 về login)
    code, _ = req("GET", "/api/subjects", want=302, follow=False)
    passed &= code in (302, 401)

    # 1. Lấy antiforgery token
    html = opener.open(BASE + "/Auth/Login").read().decode("utf-8", "replace")
    m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
    token = m.group(1) if m else ""
    print(f"[{'OK ' if token else 'FAIL'}] Antiforgery token length = {len(token)}")
    passed &= bool(token)

    # 2. Đăng nhập admin
    code, _ = req("POST", "/Auth/Login",
                  data={"Username": "admin", "Password": "admin123",
                        "__RequestVerificationToken": token})
    passed &= code in (200, 302)

    # 3. READ danh sách
    code, text = req("GET", "/api/subjects", want=200)
    passed &= code == 200
    subjects = json.loads(text) if code == 200 else []
    print(f"      -> {len(subjects)} môn: " + ", ".join(s["code"] for s in subjects))

    # 4. CREATE
    code, text = req("POST", "/api/subjects", want=201,
                     json_body={"code": "TEST101", "name": "API Test Subject", "description": "tạo bởi test"})
    new_id = json.loads(text)["id"] if code == 201 else None
    passed &= bool(code == 201 and new_id)
    print(f"      -> id mới = {new_id}")

    # 5. READ by id
    code, text = req("GET", f"/api/subjects/{new_id}", want=200)
    passed &= code == 200 and json.loads(text)["code"] == "TEST101"

    # 6. UPDATE
    code, text = req("PUT", f"/api/subjects/{new_id}", want=200,
                     json_body={"code": "TEST101", "name": "API Test (đã sửa)", "description": "updated"})
    passed &= code == 200 and json.loads(text)["name"] == "API Test (đã sửa)"

    # 7. Validation: thiếu code -> 400
    code, _ = req("POST", "/api/subjects", want=400,
                  json_body={"code": "", "name": "Thiếu mã"})
    passed &= code == 400

    # 8. DELETE
    code, _ = req("DELETE", f"/api/subjects/{new_id}", want=204)
    passed &= code == 204

    # 9. GET lại id đã xoá -> 404
    code, _ = req("GET", f"/api/subjects/{new_id}", want=404)
    passed &= code == 404

    print("\n" + ("✅ TẤT CẢ TEST PASS" if passed else "❌ CÓ TEST FAIL"))
    sys.exit(0 if passed else 1)

if __name__ == "__main__":
    main()
