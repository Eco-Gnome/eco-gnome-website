window.MapGenerator1d = {
    initialize(mapUploadButtonId) {
        console.log(mapUploadButtonId);

        const mapUploadButton = document.getElementById(mapUploadButtonId);

        mapUploadButton.addEventListener("change", (event) => {
            console.log(event);
            if (event.target.files[0]) {
                console.log('You selected ' + event.target.files[0].name);
            }
        })
    },
};

