const { spawn } = require('child_process');
const axios = require('axios');
const fs = require('fs');
const path = require('path');
const readline = require('readline');
const { Worker, isMainThread, parentPort, workerData } = require('worker_threads');

if (isMainThread) {
    // Variables en dur au début du fichier
    const assetRipperPath = 'C:\\Users\\thiba\\Downloads\\AssetRipper_win_x64\\AssetRipper.GUI.Free.exe'; // Chemin vers AssetRipper
    const unityBundlePath = 'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Eco\\Eco_Data\\StreamingAssets\\aa\\StandaloneWindows64\\icons_assets_all_77d5f1b98fd163081db5fba6aebfece2.bundle'; // Chemin vers le bundle Unity
    const exportDirectory = 'C:\\Users\\thiba\\Downloads\\test'; // Chemin vers le dossier d'exportation
    const outputDirectory = 'C:\\Users\\thiba\\Downloads\\output'; // Chemin vers le dossier de sortie final

    async function main() {
        try {
            const { serverUrl, assetRipperProcess, rl } = await startAssetRipper();

            if (serverUrl) {
                await timeoutSeconds(2);
                await loadFile(serverUrl, unityBundlePath);
                await timeoutSeconds(2);

                // Vider le dossier exportDirectory avant d'exporter dedans
                await fs.promises.rm(exportDirectory, { recursive: true, force: true });
                await fs.promises.mkdir(exportDirectory, { recursive: true });

                await exportPrimaryContent(serverUrl, exportDirectory);

                const failedItems = await processExportedFiles(exportDirectory, outputDirectory);

                if (failedItems.length > 0) {
                    console.log('Les éléments suivants ont rencontré des erreurs :');
                    for (const item of failedItems) {
                        console.log(item);
                    }
                } else {
                    console.log('Processus terminé avec succès.');
                }

                // Fermer l'interface readline
                rl.close();

                // Terminer le processus AssetRipper
                assetRipperProcess.kill();

                process.exit(0);

            } else {
                console.error('Impossible de récupérer l\'URL du serveur depuis AssetRipper.');
            }
        } catch (error) {
            console.error('Une erreur est survenue:', error);
        }
    }

    function startAssetRipper() {
        return new Promise((resolve, reject) => {
            const assetRipperProcess = spawn(assetRipperPath, [], { shell: true });

            const rl = readline.createInterface({
                input: assetRipperProcess.stdout,
                crlfDelay: Infinity
            });

            let serverUrlFound = false;

            rl.on('line', (line) => {
                console.log(line);

                const match = line.match(/Now listening on: (http:\/\/127\.0\.0\.1:\d+)/);
                if (match && !serverUrlFound) {
                    serverUrlFound = true;
                    const serverUrl = match[1];
                    console.log(`URL du serveur trouvée: ${serverUrl}`);
                    resolve({ serverUrl, assetRipperProcess, rl });
                }
            });

            assetRipperProcess.stderr.on('data', (data) => {
                console.error(`Erreur AssetRipper: ${data}`);
            });

            assetRipperProcess.on('close', (code) => {
                if (code !== 0) {
                    reject(new Error(`AssetRipper s'est terminé avec le code ${code}`));
                }
            });
        });
    }

    async function loadFile(serverUrl, filePath) {
        const formData = new URLSearchParams();
        formData.append('path', filePath);

        const response = await axios.post(`${serverUrl}/LoadFile`, formData.toString(), {
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            }
        });
        console.log('Réponse LoadFile:', response.status);
    }

    async function exportPrimaryContent(serverUrl, exportPath) {
        const formData = new URLSearchParams();
        formData.append('path', exportPath);

        const response = await axios.post(`${serverUrl}/Export/PrimaryContent`, formData.toString(), {
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            }
        });
        console.log('Réponse Export:', response.status);
    }

    async function processExportedFiles(exportPath, outputPath) {
        const iconsPath = path.join(exportPath, 'Assets', 'Art', 'UI', 'Icons');
        const tilesetImagePath = path.join(iconsPath, 'UI_Icons_Baked_0.png');

        const files = await fs.promises.readdir(iconsPath);
        const jsonFiles = files.filter(file => file.endsWith('.json'));
        const imageHeight = (await require('sharp')(tilesetImagePath).metadata()).height;

        // Créer le dossier de sortie s'il n'existe pas
        await fs.promises.mkdir(outputPath, { recursive: true });

        const failedItems = [];
        let activeWorkers = 0;
        const maxWorkers = 20;
        let index = 0;

        return new Promise((resolve) => {
            function runNext() {
                while (activeWorkers < maxWorkers && index < jsonFiles.length) {
                    const jsonFile = jsonFiles[index++];
                    activeWorkers++;

                    const jsonFilePath = path.join(iconsPath, jsonFile);
                    const outputFilePath = path.join(outputPath, `${path.basename(jsonFile, '.json')}.png`);

                    const worker = new Worker(__filename, {
                        workerData: {
                            tilesetImagePath,
                            imageHeight,
                            jsonFilePath,
                            outputFilePath,
                            jsonFileName: jsonFile
                        }
                    });

                    worker.on('message', (msg) => {
                        if (msg.success) {
                            console.log(`Image extraite pour ${msg.jsonFileName}`);
                        } else {
                            console.error(`Erreur lors de l'extraction de ${msg.jsonFileName}:`, msg.error);
                            failedItems.push(msg.jsonFileName);
                        }
                    });

                    worker.on('error', (error) => {
                        console.error(`Erreur du worker pour ${jsonFile}:`, error);
                        failedItems.push(jsonFile);
                    });

                    worker.on('exit', () => {
                        activeWorkers--;
                        if (index >= jsonFiles.length && activeWorkers === 0) {
                            resolve(failedItems);
                        } else {
                            runNext();
                        }
                    });
                }
            }

            runNext();
        });
    }

    function timeoutSeconds(sec) {
        return new Promise((resolve) => {
            setTimeout(() => {
                resolve();
            }, sec * 1000);
        });
    }

    main();

} else {
    // Code exécuté dans les workers
    const sharp = require('sharp');
    const fs = require('fs');

    (async () => {
        const { tilesetImagePath, imageHeight, jsonFilePath, outputFilePath, jsonFileName } = workerData;
        try {
            const data = await fs.promises.readFile(jsonFilePath, 'utf8');
            const jsonData = JSON.parse(data);
            const rect = jsonData.m_Rect;

            if (rect) {
                const { m_Height, m_Width, m_X, m_Y } = rect;
                const adjustedY = imageHeight - m_Y - m_Height;

                await sharp(tilesetImagePath)
                    .extract({ left: m_X, top: adjustedY, width: m_Width, height: m_Height })
                    .toFile(outputFilePath);

                parentPort.postMessage({ success: true, jsonFileName });
            } else {
                parentPort.postMessage({ success: false, error: 'Aucune propriété m_Rect trouvée', jsonFileName });
            }
        } catch (error) {
            parentPort.postMessage({ success: false, error: error.message, jsonFileName });
        }
    })();
}
