#!/usr/bin/env python3
"""Prototipo: verifica el login único contra el portal con datos de Neon.

Flujo: login con correo+clave -> /connect/authorize (code) -> /connect/token
-> compara el `sub` del id_token con el `sso_sujeto` en Neon.

Variables de entorno:
  MPAZ_SSO_CONTRASENA  Clave del funcionario de prueba (obligatoria).
  MPAZ_SSO_SECRETO     Secreto del cliente-prueba (obligatorio).
  MPAZ_SSO_CORREO      Correo de prueba (por defecto, el de Administración TI).
  MPAZ_SSO_BASE        URL del portal (por defecto http://localhost:5195).
  MPAZ_SSO_RETORNO     Redirect URI registrado (por defecto el de Development).
  MPAZ_DB_CONEXION     URI PostgreSQL de Neon (obligatoria, misma base que el portal).
"""
import base64
import json
import os
import re
import sys
import urllib.parse

import psycopg
import requests

CONEXION = os.environ.get("MPAZ_DB_CONEXION", "").strip()
if not CONEXION:
    sys.exit("Defina MPAZ_DB_CONEXION con la URI de Neon antes de ejecutar la prueba.")

BASE = os.environ.get("MPAZ_SSO_BASE", "http://localhost:5195")
CORREO = os.environ.get("MPAZ_SSO_CORREO", "admin.palma@mpaz.cl")
CONTRASENA = os.environ["MPAZ_SSO_CONTRASENA"]
CLIENTE = os.environ.get("MPAZ_SSO_CLIENTE", "cliente-prueba")
SECRETO = os.environ["MPAZ_SSO_SECRETO"]
RETORNO = os.environ.get("MPAZ_SSO_RETORNO", "http://127.0.0.1:5199/callback")

s = requests.Session()

# 1. Login en el portal.
r = s.get(BASE + "/Cuenta/Login", timeout=15)
r.raise_for_status()
m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', r.text)
if not m:
    sys.exit("No se encontró el token antiforgery del login")
r = s.post(BASE + "/Cuenta/Login",
           data={"Correo": CORREO, "Contrasena": CONTRASENA,
                 "__RequestVerificationToken": m.group(1)},
           timeout=15)
if "Correo o contraseña incorrectos" in r.text or "Ingrese su contrase" in r.text:
    sys.exit("Login rechazado: revise correo y MPAZ_SSO_CONTRASENA")
print("1. Login en el portal: OK (%s)" % r.url)

# 2. Autorización: se espera el código en la redirección (no se sigue).
r = s.get(BASE + "/connect/authorize", params={
    "client_id": CLIENTE, "redirect_uri": RETORNO, "response_type": "code",
    "scope": "openid profile email mpaz_api"}, allow_redirects=False, timeout=15)
if r.status_code not in (301, 302) or "Location" not in r.headers:
    sys.exit("Autorización falló: HTTP %d %s" % (r.status_code, r.text[:200]))
partes = urllib.parse.urlparse(r.headers["Location"])
codigo = urllib.parse.parse_qs(partes.query).get("code", [None])[0]
if not codigo:
    sys.exit("Sin código en la redirección: " + r.headers["Location"][:200])
print("2. Código de autorización: OK")

# 3. Canje por tokens.
r = s.post(BASE + "/connect/token", data={
    "grant_type": "authorization_code", "code": codigo,
    "redirect_uri": RETORNO, "client_id": CLIENTE, "client_secret": SECRETO},
    timeout=15)
if r.status_code != 200:
    sys.exit("Token falló: HTTP %d %s" % (r.status_code, r.text[:300]))
tokens = r.json()
print("3. Tokens recibidos: %s" % sorted(tokens.keys()))

# 4. El sub del id_token debe ser el sso_sujeto en Neon.
payload = tokens["id_token"].split(".")[1]
payload += "=" * (-len(payload) % 4)
sub = json.loads(base64.urlsafe_b64decode(payload))["sub"]
with psycopg.connect(CONEXION, connect_timeout=15) as c:
    c.execute("SET TRANSACTION READ ONLY")
    esperado = c.execute(
        "select sso_sujeto from mpaz_matricula.funcionario where lower(correo) = %s",
        (CORREO.lower(),)).fetchone()[0]
print("   sub del token : %s" % sub)
print("   sso_sujeto BD : %s" % esperado)
if sub != esperado:
    sys.exit("NO COINCIDEN")
print("4. sub == sso_sujeto: OK — login único verificado")
