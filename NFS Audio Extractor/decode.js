const fs = require('fs');
const path = require('path');
const { execFile } = require('child_process');

const executables = {
  '.abk': path.resolve(__dirname, './bin/NFS_abk_decode.exe'),
  '.gin': path.resolve(__dirname, './bin/NFS_gin_decode.exe'),
};

fs.readdirSync('./source').forEach(file => {
  const extension = path.extname(file);
  const filename = path.basename(file, extension);

  if (executables[extension]) {
    console.log(`file: ${file}`);
    fs.mkdirSync(`./dist/${filename}`, { recursive: true });

    const fileAbsolutePath = path.resolve(__dirname, `./source/${file}`);
    const destAbsolutePath = path.resolve(__dirname, `./dist/${filename}`);
    
    execFile(executables[extension], [fileAbsolutePath], (err, stdout, stderr) => {
      if (err) {
        console.log(err);
        return;
      }
  
      console.log(`stdout: ${stdout}`);
      console.log(`stderr: ${stderr}`);

      fs.readdirSync('./').forEach(file => {
        if (file.indexOf(`${filename}`) !== -1 && file.indexOf('.wav') !== -1) {
          fs.renameSync(`./${file}`, `./dist/${filename}/${file}`);
        }
      });
    });
  }
});
