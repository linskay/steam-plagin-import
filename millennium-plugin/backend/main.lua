local millennium = require("millennium")
local cjson = require("json")
local fs = require("fs")
local ffi = require("ffi")
ffi.cdef[[
int MultiByteToWideChar(unsigned int, unsigned long, const char*, int, unsigned short*, int);
intptr_t ShellExecuteW(void*, const unsigned short*, const unsigned short*, const unsigned short*, const unsigned short*, int);
int MoveFileExW(const unsigned short*, const unsigned short*, unsigned long);
]]
local kernel = ffi.load("kernel32")
local shell = ffi.load("shell32")
local function wide(text)
    local count = kernel.MultiByteToWideChar(65001, 0, text, #text, nil, 0)
    local buffer = ffi.new("unsigned short[?]", count + 1)
    kernel.MultiByteToWideChar(65001, 0, text, #text, buffer, count)
    return buffer, count
end
local function settings_path() return millennium.steam_path() .. "/config/SteamEpicSync/settings.json" end
local function settings()
    local file = io.open(settings_path(), "rb")
    if not file then return {LaunchWithSteam=true, SyncOnSteamStart=false, CollectionName="Есть в EGS"} end
    local raw = file:read("*a"); file:close()
    return cjson.decode(raw)
end

---@ffi
---@return string
function getSettings() return cjson.encode(settings()) end

---@ffi
---@param launch boolean
---@param sync boolean
---@param name string
---@return string
function saveSettings(launch, sync, name)
    name = name:match("^%s*(.-)%s*$")
    local _, length = wide(name)
    if length == 0 or length > 80 or name:find("[%z\r\n]") then error("Название коллекции: от 1 до 80 символов без переносов строк.") end
    local value = settings()
    value.LaunchWithSteam = launch
    value.SyncOnSteamStart = sync
    value.CollectionName = name
    local path = settings_path()
    fs.create_directories(millennium.steam_path() .. "/config/SteamEpicSync")
    local file = assert(io.open(path .. ".lua.tmp", "wb"))
    file:write(cjson.encode(value)); file:close()
    assert(kernel.MoveFileExW(wide(path .. ".lua.tmp"), wide(path), 9) ~= 0, "Не удалось сохранить настройки.")
    return cjson.encode(value)
end

local function start_with_steam()
    local value = settings()
    if not value.LaunchWithSteam and not value.SyncOnSteamStart then return end
    local exe = value.ExePath
    if type(exe) ~= "string" or not exe:lower():match("steamepicsync%.exe$") or not fs.is_file(exe) then return end
    local result = shell.ShellExecuteW(nil, nil, wide(exe), wide("--steam-bootstrap"), nil, 0)
    assert(tonumber(result) > 32, "Не удалось запустить Steam Epic Sync.")
end

-- Read exactly one local snapshot on plugin startup. No polling, credentials or network.
---@ffi
---@param accountId string
---@return string
function getSnapshot(accountId)
    if type(accountId) ~= "string" or not accountId:match("^%d+$") or #accountId > 12 then
        return '{"schemaVersion":1,"games":[]}'
    end
    local path = millennium.steam_path() .. "/config/SteamEpicSync/" .. accountId .. ".json"
    local file = io.open(path, "rb")
    if not file then return '{"schemaVersion":1,"games":[]}' end
    local content = file:read(8 * 1024 * 1024 + 1)
    file:close()
    if not content or #content > 8 * 1024 * 1024 then return '{"schemaVersion":1,"games":[]}' end
    return content
end

return {
    on_load = function() millennium.ready() end,
    on_frontend_loaded = function() local ok, err = pcall(start_with_steam); if not ok then print("SteamEpicSync startup: " .. tostring(err)) end end,
    on_unload = function() end
}
