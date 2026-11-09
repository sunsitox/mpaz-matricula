@echo off
setlocal

echo ============================================
echo  Generando solucion MarcelaPaz.Matriculas
echo ============================================
echo.

REM 1. Detectar framework objetivo (net8.0 preferido, fallback net10.0 / net9.0)
echo [0/8] Detectando framework disponible...
set "FRAMEWORK=net10.0"
dotnet new list --framework net8.0 >/dev/null 2>&1
if %errorlevel% equ 0 (
    set "FRAMEWORK=net8.0"
    echo   - net8.0 disponible.
) else (
    dotnet new list --framework net10.0 >/dev/null 2>&1
    if %errorlevel% equ 0 (
        set "FRAMEWORK=net10.0"
        echo   - net10.0 disponible.
    ) else (
        dotnet new list --framework net9.0 >/dev/null 2>&1
        if %errorlevel% equ 0 (
            set "FRAMEWORK=net9.0"
            echo   - net9.0 disponible.
        ) else (
            echo.
            echo ERROR: Ningun framework compatible detectado (net8.0, net9.0, net10.0).
            echo Instala las workloads/templates necesarios para al menos uno de ellos.
            exit /b 1
        )
    )
)
echo   - Usando framework: %%FRAMEWORK%%
echo.

REM 2. Crear la solucion en blanco
echo [1/8] Preparando solucion en blanco...
if exist "MarcelaPaz.Matriculas.sln" (
    echo   - La solucion ya existe. Se recreara con --force.
    dotnet new sln -n MarcelaPaz.Matriculas --force
) else if exist "MarcelaPaz.Matriculas.slnx" (
    echo   - La solucion .slnx ya existe. Se recreara con --force.
    dotnet new sln -n MarcelaPaz.Matriculas --force
) else (
    echo   - Creando solucion nueva.
    dotnet new sln -n MarcelaPaz.Matriculas
)
if %errorlevel% neq 0 (
    echo ERROR: No se pudo crear la solucion.
    exit /b %errorlevel%
)
echo [+] Solucion lista.
echo.

REM 3. Crear los 4 proyectos
echo [2/8] Creando proyectos...

echo   - MarcelaPaz.Domain...
dotnet new classlib -n MarcelaPaz.Domain -f %%FRAMEWORK%%
if %errorlevel% neq 0 (echo ERROR al crear Domain & exit /b %errorlevel%)
echo   [+] Domain creado.

echo   - MarcelaPaz.Application...
dotnet new classlib -n MarcelaPaz.Application -f %%FRAMEWORK%%
if %errorlevel% neq 0 (echo ERROR al crear Application & exit /b %errorlevel%)
echo   [+] Application creado.

echo   - MarcelaPaz.Infrastructure...
dotnet new classlib -n MarcelaPaz.Infrastructure -f %%FRAMEWORK%%
if %errorlevel% neq 0 (echo ERROR al crear Infrastructure & exit /b %errorlevel%)
echo   [+] Infrastructure creado.

echo   - MarcelaPaz.Web (ASP.NET Core Web API)...
dotnet new webapi -n MarcelaPaz.Web -f %%FRAMEWORK%%
if %errorlevel% neq 0 (echo ERROR al crear Web & exit /b %errorlevel%)
echo   [+] Web creado.

echo.
echo [+] Proyectos creados correctamente.
echo.

REM 4. Agregar proyectos a la solucion
echo [3/8] Agregando proyectos a la solucion...
dotnet sln MarcelaPaz.Matriculas.sln add MarcelaPaz.Domain/MarcelaPaz.Domain.csproj
dotnet sln MarcelaPaz.Matriculas.sln add MarcelaPaz.Application/MarcelaPaz.Application.csproj
dotnet sln MarcelaPaz.Matriculas.sln add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj
dotnet sln MarcelaPaz.Matriculas.sln add MarcelaPaz.Web/MarcelaPaz.Web.csproj
echo [+] Proyectos agregados a la solucion.
echo.

REM 5. Referencias entre proyectos (Clean Architecture)
echo [4/8] Configurando referencias entre proyectos...

echo   - Domain: sin referencias (capa base)

echo   - Application -> Domain...
dotnet add MarcelaPaz.Application/MarcelaPaz.Application.csproj reference MarcelaPaz.Domain/MarcelaPaz.Domain.csproj
echo   [+] Application referencia Domain.

echo   - Infrastructure -> Application, Domain...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj reference MarcelaPaz.Application/MarcelaPaz.Application.csproj
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj reference MarcelaPaz.Domain/MarcelaPaz.Domain.csproj
echo   [+] Infrastructure referencia Application y Domain.

echo   - Web -> Application, Infrastructure...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj reference MarcelaPaz.Application/MarcelaPaz.Application.csproj
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj reference MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj
echo   [+] Web referencia Application e Infrastructure.

echo.
echo [+] Referencias configuradas correctamente.
echo.

REM 6. Instalar paquetes NuGet en Domain
echo [5/8] Instalando paquetes NuGet en Domain...
dotnet add MarcelaPaz.Domain/MarcelaPaz.Domain.csproj package Microsoft.EntityFrameworkCore.Abstractions
if %errorlevel% neq 0 (echo WARNING: No se pudo instalar Microsoft.EntityFrameworkCore.Abstractions en Domain)
echo   [+] Domain: Microsoft.EntityFrameworkCore.Abstractions instalado.
echo.

REM 7. Instalar paquetes NuGet en Application
echo [6/8] Instalando paquetes NuGet en Application...
dotnet add MarcelaPaz.Application/MarcelaPaz.Application.csproj package Microsoft.EntityFrameworkCore.Abstractions
if %errorlevel% neq 0 (echo WARNING: No se pudo instalar Microsoft.EntityFrameworkCore.Abstractions en Application)
echo   [+] Application: Microsoft.EntityFrameworkCore.Abstractions instalado.
echo.

REM 8. Instalar paquetes NuGet en Infrastructure
echo [7/8] Instalando paquetes NuGet en Infrastructure...
echo   - Microsoft.EntityFrameworkCore...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.EntityFrameworkCore
echo   - Microsoft.EntityFrameworkCore.Design...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.EntityFrameworkCore.Design
echo   - Npgsql.EntityFrameworkCore.PostgreSQL...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
echo   - Microsoft.AspNetCore.Identity.EntityFrameworkCore...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
echo   - Microsoft.Extensions.DependencyInjection...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.Extensions.DependencyInjection
echo   - Microsoft.Extensions.Configuration.Abstractions...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.Extensions.Configuration.Abstractions
echo   - Microsoft.Extensions.Options...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.Extensions.Options
echo   - Microsoft.Extensions.Logging.Abstractions...
dotnet add MarcelaPaz.Infrastructure/MarcelaPaz.Infrastructure.csproj package Microsoft.Extensions.Logging.Abstractions
echo.
echo [+] Infrastructure: paquetes instalados.
echo.

REM 9. Instalar paquetes NuGet en Web
echo [8/8] Instalando paquetes NuGet en Web...
echo   - Microsoft.EntityFrameworkCore.Design...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Microsoft.EntityFrameworkCore.Design
echo   - Npgsql.EntityFrameworkCore.PostgreSQL...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Npgsql.EntityFrameworkCore.PostgreSQL
echo   - Microsoft.AspNetCore.Identity.EntityFrameworkCore...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Microsoft.AspNetCore.Identity.EntityFrameworkCore
echo   - Microsoft.Extensions.Configuration.EnvironmentVariables...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Microsoft.Extensions.Configuration.EnvironmentVariables
echo   - Microsoft.Extensions.Configuration.Json...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Microsoft.Extensions.Configuration.Json
echo   - BCrypt.Net-Next...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package BCrypt.Net-Next
echo   - Serilog.AspNetCore...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package Serilog.AspNetCore
echo   - FluentValidation.AspNetCore...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package FluentValidation.AspNetCore
echo   - MediatR...
dotnet add MarcelaPaz.Web/MarcelaPaz.Web.csproj package MediatR
echo.
echo [+] Web: paquetes instalados.
echo.

REM 10. Crear estructura de carpetas sugerida para Clean Architecture
echo [Final] Creando estructura de carpetas...

mkdir MarcelaPaz.Domain\Entities
mkdir MarcelaPaz.Domain\ValueObjects
mkdir MarcelaPaz.Domain\Enums
mkdi
