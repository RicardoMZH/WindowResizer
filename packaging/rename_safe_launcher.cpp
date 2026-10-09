#include <windows.h>
#include <bcrypt.h>
#include <shellapi.h>
#include <shlobj.h>

#include <array>
#include <cstdint>
#include <cstring>
#include <cwchar>
#include <string>
#include <vector>

#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "shell32.lib")
#pragma comment(lib, "user32.lib")

namespace
{
constexpr int kPayloadResourceId = 101;
constexpr wchar_t kStableExecutableName[] = L"WindowSizerWinUI.exe";

void ShowError(const wchar_t* message)
{
    MessageBoxW(nullptr, message, L"Window Sizer", MB_OK | MB_ICONERROR);
}

std::wstring GetDirectoryName(const std::wstring& path)
{
    const auto separator = path.find_last_of(L"\\/");
    return separator == std::wstring::npos ? L"." : path.substr(0, separator);
}

std::wstring GetLauncherDirectory()
{
    std::vector<wchar_t> buffer(32768);
    const DWORD length = GetModuleFileNameW(
        nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (length == 0 || length >= buffer.size())
        return L".";
    return GetDirectoryName(std::wstring(buffer.data(), length));
}

bool HashPayload(
    const std::uint8_t* data,
    const DWORD size,
    std::array<std::uint8_t, 32>& digest)
{
    BCRYPT_ALG_HANDLE algorithm = nullptr;
    BCRYPT_HASH_HANDLE hash = nullptr;
    DWORD objectSize = 0;
    DWORD resultSize = 0;
    std::vector<std::uint8_t> object;

    if (BCryptOpenAlgorithmProvider(
            &algorithm, BCRYPT_SHA256_ALGORITHM, nullptr, 0) < 0)
        return false;
    if (BCryptGetProperty(
            algorithm, BCRYPT_OBJECT_LENGTH,
            reinterpret_cast<PUCHAR>(&objectSize), sizeof(objectSize),
            &resultSize, 0) < 0)
        goto cleanup;

    object.resize(objectSize);
    if (BCryptCreateHash(
            algorithm, &hash, object.data(), objectSize, nullptr, 0, 0) < 0)
        goto cleanup;
    if (BCryptHashData(hash, const_cast<PUCHAR>(data), size, 0) < 0)
        goto cleanup;
    if (BCryptFinishHash(
            hash, digest.data(), static_cast<ULONG>(digest.size()), 0) < 0)
        goto cleanup;

    BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    return true;

cleanup:
    if (hash != nullptr)
        BCryptDestroyHash(hash);
    BCryptCloseAlgorithmProvider(algorithm, 0);
    return false;
}

std::wstring DigestPrefix(const std::array<std::uint8_t, 32>& digest)
{
    constexpr wchar_t digits[] = L"0123456789abcdef";
    std::wstring value;
    value.reserve(24);
    for (std::size_t index = 0; index < 12; ++index)
    {
        value.push_back(digits[digest[index] >> 4]);
        value.push_back(digits[digest[index] & 0x0f]);
    }
    return value;
}

bool FileMatchesPayload(
    const std::wstring& path,
    const std::uint8_t* payload,
    const DWORD payloadSize)
{
    WIN32_FILE_ATTRIBUTE_DATA attributes{};
    if (!GetFileAttributesExW(
            path.c_str(), GetFileExInfoStandard, &attributes))
        return false;
    const std::uint64_t actual =
        (static_cast<std::uint64_t>(attributes.nFileSizeHigh) << 32) |
        attributes.nFileSizeLow;
    if (actual != payloadSize)
        return false;

    HANDLE file = CreateFileW(
        path.c_str(), GENERIC_READ,
        FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
        nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return false;

    std::vector<std::uint8_t> buffer(4 * 1024 * 1024);
    DWORD offset = 0;
    bool matches = true;
    while (offset < payloadSize)
    {
        const DWORD remaining = payloadSize - offset;
        const DWORD chunk = remaining < buffer.size()
            ? remaining
            : static_cast<DWORD>(buffer.size());
        DWORD read = 0;
        if (!ReadFile(file, buffer.data(), chunk, &read, nullptr) ||
            read != chunk ||
            std::memcmp(buffer.data(), payload + offset, chunk) != 0)
        {
            matches = false;
            break;
        }
        offset += read;
    }
    CloseHandle(file);
    return matches;
}

bool WritePayload(
    const std::wstring& destination,
    const std::uint8_t* data,
    const DWORD size)
{
    const std::wstring temporary =
        destination + L".tmp." + std::to_wstring(GetCurrentProcessId());
    HANDLE file = CreateFileW(
        temporary.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
        FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return false;

    DWORD offset = 0;
    bool success = true;
    while (offset < size)
    {
        const DWORD remaining = size - offset;
        const DWORD chunk =
            remaining < 4 * 1024 * 1024 ? remaining : 4 * 1024 * 1024;
        DWORD written = 0;
        if (!WriteFile(file, data + offset, chunk, &written, nullptr) ||
            written != chunk)
        {
            success = false;
            break;
        }
        offset += written;
    }
    if (success)
        success = FlushFileBuffers(file) != FALSE;
    CloseHandle(file);

    if (success)
    {
        success = MoveFileExW(
            temporary.c_str(), destination.c_str(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH) != FALSE;
    }
    if (!success)
        DeleteFileW(temporary.c_str());
    return success;
}

std::wstring QuoteArgument(const std::wstring& argument)
{
    if (argument.empty())
        return L"\"\"";
    if (argument.find_first_of(L" \t\n\v\"") == std::wstring::npos)
        return argument;

    std::wstring quoted = L"\"";
    std::size_t slashes = 0;
    for (const wchar_t character : argument)
    {
        if (character == L'\\')
        {
            ++slashes;
            continue;
        }
        if (character == L'\"')
        {
            quoted.append(slashes * 2 + 1, L'\\');
            quoted.push_back(L'\"');
            slashes = 0;
            continue;
        }
        quoted.append(slashes, L'\\');
        slashes = 0;
        quoted.push_back(character);
    }
    quoted.append(slashes * 2, L'\\');
    quoted.push_back(L'\"');
    return quoted;
}

std::wstring BuildCommandLine(const std::wstring& executable)
{
    std::wstring command = QuoteArgument(executable);
    int count = 0;
    LPWSTR* arguments = CommandLineToArgvW(GetCommandLineW(), &count);
    if (arguments == nullptr)
        return command;

    for (int index = 1; index < count; ++index)
    {
        command.push_back(L' ');
        command += QuoteArgument(arguments[index]);
    }
    LocalFree(arguments);
    return command;
}

bool RemoveCacheDirectory(const std::wstring& directory)
{
    WIN32_FIND_DATAW entry{};
    HANDLE search = FindFirstFileW((directory + L"\\*").c_str(), &entry);
    if (search == INVALID_HANDLE_VALUE)
        return GetLastError() == ERROR_FILE_NOT_FOUND &&
            RemoveDirectoryW(directory.c_str()) != FALSE;

    bool success = true;
    do
    {
        if (std::wcscmp(entry.cFileName, L".") == 0 ||
            std::wcscmp(entry.cFileName, L"..") == 0)
            continue;

        const std::wstring path = directory + L"\\" + entry.cFileName;
        if ((entry.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
        {
            // A junction must not lead cleanup outside this payload's cache.
            if ((entry.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
                success = RemoveDirectoryW(path.c_str()) != FALSE && success;
            else
                success = RemoveCacheDirectory(path) && success;
        }
        else
        {
            success = DeleteFileW(path.c_str()) != FALSE && success;
        }
    } while (FindNextFileW(search, &entry));
    const DWORD findError = GetLastError();
    FindClose(search);
    return findError == ERROR_NO_MORE_FILES &&
        RemoveDirectoryW(directory.c_str()) != FALSE && success;
}

bool LockRuntime(HANDLE mutex)
{
    const DWORD result = WaitForSingleObject(mutex, INFINITE);
    return result == WAIT_OBJECT_0 || result == WAIT_ABANDONED;
}
}

int WINAPI wWinMain(HINSTANCE, HINSTANCE, PWSTR, int)
{
    const HMODULE module = GetModuleHandleW(nullptr);
    const HRSRC resource = FindResourceW(
        module, MAKEINTRESOURCEW(kPayloadResourceId), RT_RCDATA);
    if (resource == nullptr)
    {
        ShowError(L"Application resource is incomplete.");
        return 1;
    }

    const HGLOBAL loaded = LoadResource(module, resource);
    const DWORD payloadSize = SizeofResource(module, resource);
    const auto* payload =
        static_cast<const std::uint8_t*>(LockResource(loaded));
    if (loaded == nullptr || payload == nullptr || payloadSize == 0)
    {
        ShowError(L"Failed to read application resource.");
        return 2;
    }

    std::array<std::uint8_t, 32> digest{};
    if (!HashPayload(payload, payloadSize, digest))
    {
        ShowError(L"Failed to validate application resource.");
        return 3;
    }

    // The override keeps release tests isolated; normal launches use LocalAppData.
    std::wstring runtimeRoot;
    std::vector<wchar_t> overrideBuffer(32768);
    const DWORD overrideLength = GetEnvironmentVariableW(
        L"WINDOWSIZER_RUNTIME_ROOT", overrideBuffer.data(),
        static_cast<DWORD>(overrideBuffer.size()));
    if (overrideLength > 0 && overrideLength < overrideBuffer.size())
    {
        runtimeRoot.assign(overrideBuffer.data(), overrideLength);
    }
    else
    {
        std::vector<wchar_t> localBuffer(32768);
        const DWORD localLength = GetEnvironmentVariableW(
            L"LOCALAPPDATA", localBuffer.data(),
            static_cast<DWORD>(localBuffer.size()));
        if (localLength > 0 && localLength < localBuffer.size())
        {
            runtimeRoot.assign(localBuffer.data(), localLength);
        }
        else
        {
            PWSTR localAppData = nullptr;
            if (FAILED(SHGetKnownFolderPath(
                    FOLDERID_LocalAppData, KF_FLAG_CREATE, nullptr, &localAppData)))
            {
                ShowError(L"Local application data directory is unavailable.");
                return 4;
            }
            runtimeRoot.assign(localAppData);
            CoTaskMemFree(localAppData);
        }
        runtimeRoot += L"\\WindowSizerWinUI\\Runtime";
    }

    const std::wstring digestPrefix = DigestPrefix(digest);
    const std::wstring runtimeDirectory = runtimeRoot + L"\\" + digestPrefix;
    const std::wstring mutexName =
        L"Global\\WindowSizerWinUI.Runtime." + digestPrefix;
    HANDLE mutex = CreateMutexW(nullptr, FALSE, mutexName.c_str());
    if (mutex == nullptr || !LockRuntime(mutex))
    {
        if (mutex != nullptr)
            CloseHandle(mutex);
        ShowError(L"Failed to coordinate application cache.");
        return 5;
    }

    const int directoryResult =
        SHCreateDirectoryExW(nullptr, runtimeDirectory.c_str(), nullptr);
    if (directoryResult != ERROR_SUCCESS &&
        directoryResult != ERROR_ALREADY_EXISTS &&
        directoryResult != ERROR_FILE_EXISTS)
    {
        ReleaseMutex(mutex);
        CloseHandle(mutex);
        ShowError(L"Failed to create application cache directory.");
        return 5;
    }

    // Shared handles mark live launchers. Cleanup takes an exclusive handle.
    const std::wstring leasePath = runtimeDirectory + L"\\.lease";
    HANDLE lease = CreateFileW(
        leasePath.c_str(), GENERIC_READ | GENERIC_WRITE,
        FILE_SHARE_READ | FILE_SHARE_WRITE,
        nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (lease == INVALID_HANDLE_VALUE)
    {
        ReleaseMutex(mutex);
        CloseHandle(mutex);
        ShowError(L"Failed to reserve application cache.");
        return 5;
    }

    const std::wstring bundleDirectory = runtimeDirectory + L"\\Bundle";
    const int bundleResult =
        SHCreateDirectoryExW(nullptr, bundleDirectory.c_str(), nullptr);
    if ((bundleResult != ERROR_SUCCESS &&
         bundleResult != ERROR_ALREADY_EXISTS &&
         bundleResult != ERROR_FILE_EXISTS) ||
        !SetEnvironmentVariableW(
            L"DOTNET_BUNDLE_EXTRACT_BASE_DIR", bundleDirectory.c_str()))
    {
        CloseHandle(lease);
        ReleaseMutex(mutex);
        CloseHandle(mutex);
        ShowError(L"Failed to prepare application extraction directory.");
        return 5;
    }

    const std::wstring executable =
        runtimeDirectory + L"\\" + kStableExecutableName;
    if (!FileMatchesPayload(executable, payload, payloadSize) &&
        (!WritePayload(executable, payload, payloadSize) ||
         !FileMatchesPayload(executable, payload, payloadSize)))
    {
        CloseHandle(lease);
        ReleaseMutex(mutex);
        CloseHandle(mutex);
        ShowError(L"Failed to extract application payload.");
        return 6;
    }

    std::wstring commandLine = BuildCommandLine(executable);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    PROCESS_INFORMATION process{};
    const std::wstring workingDirectory = GetLauncherDirectory();
    if (!CreateProcessW(
            executable.c_str(), commandLine.data(), nullptr, nullptr, FALSE,
            0, nullptr, workingDirectory.c_str(), &startup, &process))
    {
        CloseHandle(lease);
        ReleaseMutex(mutex);
        CloseHandle(mutex);
        ShowError(L"Failed to start application.");
        return 7;
    }

    ReleaseMutex(mutex);
    CloseHandle(process.hThread);
    WaitForSingleObject(process.hProcess, INFINITE);
    CloseHandle(process.hProcess);

    if (LockRuntime(mutex))
    {
        CloseHandle(lease);
        HANDLE exclusive = CreateFileW(
            leasePath.c_str(), GENERIC_READ | GENERIC_WRITE, 0,
            nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (exclusive != INVALID_HANDLE_VALUE)
        {
            CloseHandle(exclusive);
            RemoveCacheDirectory(runtimeDirectory);
        }
        ReleaseMutex(mutex);
    }
    else
    {
        CloseHandle(lease);
    }
    CloseHandle(mutex);
    return 0;
}
