const fs = require('fs');
const path = require('path');

// À modifier selon tes dossiers
const dossierA = 'C:\\Users\\thiba\\Documents\\Repositories\\eco-calculator-website\\ecocraft\\Exports\\OfficialIcons';
const dossierB = 'C:\\Users\\thiba\\Documents\\Repositories\\eco-calculator-website\\ecocraft\\wwwroot\\assets\\eco-icons';

function listFiles(dir) {
    return fs.readdirSync(dir).filter(file => fs.statSync(path.join(dir, file)).isFile());
}

const filesA = listFiles(dossierA);
const filesB = listFiles(dossierB);

const onlyInA = filesA.filter(f => !filesB.includes(f));
const onlyInB = filesB.filter(f => !filesA.includes(f));

console.log('Fichiers présents **seulement** dans dossierA :');
console.log(onlyInA.length ? onlyInA : 'Aucun');
console.log('\nFichiers présents **seulement** dans dossierB :');
console.log(onlyInB.length ? onlyInB : 'Aucun');

// Copie les fichiers manquants dans B
onlyInA.forEach(filename => {
    const src = path.join(dossierA, filename);
    const dest = path.join(dossierB, filename);
    fs.copyFileSync(src, dest);
    console.log(`Copié : ${filename}`);
});

console.log('\nCopie terminée.');
