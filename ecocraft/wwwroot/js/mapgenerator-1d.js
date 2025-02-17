window.MapGenerator1d = {
    importedImages: new Map(),
    initialize(blazorComponent, mapUploadButtonId, contributionNames) {
        this.blazorComponent = blazorComponent;
        this.contributionNames = contributionNames;
        const mapUploadButton = document.getElementById(mapUploadButtonId);

        mapUploadButton.addEventListener("change", async (event) => {
            console.log(event.target.files);
            this.importedImages.clear();

            if (!Array.from(event.target.files).find(f => f.name === "Biomes.png")) {
                this.blazorComponent.invokeMethodAsync("WarnUser", "NO_BIOMES_FILE");
                return;
            }

            let size;

            for (let file of event.target.files) {
                console.log(file);
                let name = file.name.split('.')[0];

                if (file.type !== "image/png") {
                    this.blazorComponent.invokeMethodAsync("WarnUser", "WRONG_FILE_TYPE", file.name);
                    continue;
                }

                if (!this.contributionNames.includes(name)) {
                    this.blazorComponent.invokeMethodAsync("WarnUser", "UNABLE_TO_PROCESS", file.name);
                    continue;
                }

                let resolver;
                let promise = new Promise((resolve) => {
                    resolver = resolve;
                });

                // Lecture du fichier comme URL de données
                let reader = new FileReader();
                reader.onload = (e) => {
                    let img = new Image();

                    img.onload = () => {
                        console.log(`Image ${file.name} - Largeur: ${img.width}, Hauteur: ${img.height}`);

                        if (img.width !== img.height) {
                            this.blazorComponent.invokeMethodAsync("WarnUser", "IMAGE_NOT_SQUARE", file.name);
                            resolver();
                            return;
                        }

                        if (!size) {
                            size = img.width;
                        }

                        if (size !== img.width) {
                            this.blazorComponent.invokeMethodAsync("WarnUser", "IMAGE_WRONG_SIZE", file.name);
                            resolver();
                            return;
                        }

                        this.importedImages.set(name, img);

                        this.blazorComponent.invokeMethodAsync("WarnUser", "PROCESS_SUCCESS", file.name);
                        resolver();
                    };

                    img.src = e.target.result;
                };

                reader.readAsDataURL(file);

                await promise;
            }

            this.blazorComponent.invokeMethodAsync("ImportMap", size);
        })
    },
    downloadImages(imageIds) {
        for (let imageId of imageIds) {
            const image = document.getElementById(imageId);
            const link = document.createElement('a');
            link.href = image.src;
            link.download = imageId.replace('Image', '') + '.png';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        }
    }
};

