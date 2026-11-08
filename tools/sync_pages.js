const fs = require('fs');
const path = require('path');

const pages = [
    'AddNewUserQde.razor',
    'GroupSetup.razor',
    'GroupPageSetup.razor',
    'UserManage.razor',
    'Admin.razor'
];

const webPagesDir = path.resolve(__dirname, '../Components/Pages');
const mobilePagesDir = path.resolve(__dirname, '../HRsuiteRazor.Mobile/Components/Pages');

for (const p of pages) {
    const src = path.join(webPagesDir, p);
    const dest = path.join(mobilePagesDir, p);
    if (fs.existsSync(src)) {
        let content = fs.readFileSync(src, 'utf8');
        content = content.replace(/@rendermode\s+InteractiveServer\r?\n/, '');
        fs.writeFileSync(dest, content, 'utf8');
        console.log(`Synced ${p} -> Mobile`);
    }
}
