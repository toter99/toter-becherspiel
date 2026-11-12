fx_version 'cerulean'
game 'gta5'

author 'toter'
description 'Becherspiel (Hütchenspiel) für Gangs: Tisch aufstellen, Wetten, gezinkte Runden'
version '1.0.0'
repository 'https://github.com/toter99/toter-becherspiel'

dependency 'qb-core'

client_script 'Client/bin/Release/*.net.dll'
server_script 'Server/bin/Release/*.net.dll'

ui_page 'html/index.html'

files {
    'html/index.html',
    'html/*.css',
    'html/*.js'
}
