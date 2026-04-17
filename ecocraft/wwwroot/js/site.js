function triggerFileDownload(fileName, base64Content) {
    const link = document.createElement("a");
    link.href = "data:application/octet-stream;base64," + base64Content;
    link.download = fileName;
    link.click();
    link.remove();
}

function scrollElementIntoViewCenter(element) {
    if (!element || typeof element.scrollIntoView !== "function") {
        return;
    }

    element.scrollIntoView({
        behavior: "smooth",
        block: "center",
        inline: "nearest"
    });
}
