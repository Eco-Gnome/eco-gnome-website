window.MapGenerator2d = {
    initialize(canvasNames, size, canvasContainer, currentTool, toolSize, toolColor) {
        console.log("Initialize MapGenerator2d");
        this.size = size;
        this.container = document.getElementById(canvasContainer);
        this.currentTool = currentTool;
        this.toolSize = toolSize;
        this.toolColor = toolColor;

        this.allCanvasObj = canvasNames.map(name => ({canvas: document.getElementById(name), name})).map(({canvas, name}) => {
            const context = canvas.getContext("2d", { willReadFrequently: true });
            context.imageSmoothingEnabled = false;

            canvas.width = this.container.clientWidth;
            canvas.height = this.container.clientHeight;

            const bufferCanvas = document.createElement("canvas");
            bufferCanvas.width = this.size;
            bufferCanvas.height = this.size;
            const bufferContext = bufferCanvas.getContext("2d", { willReadFrequently: true });
            bufferContext.imageSmoothingEnabled = false;

            return { name, canvas, context, bufferCanvas, bufferContext };
        });

        this.activeCanvasObj = this.allCanvasObj[0];

        this.isPanning = false;
        this.isDrawing = false;
        this.offsetX = 0;
        this.offsetY = 0;
        this.startX = 0;
        this.startY = 0;

        if (this.container.clientWidth < this.container.clientHeight)
        {
            this.maxScale = Math.round(this.container.clientHeight / this.size * 100) / 100;
            this.minScale = Math.round(this.container.clientWidth / this.size * 100) / 100;
        }
        else
        {
            this.maxScale = Math.round(this.container.clientWidth / this.size * 100) / 100;
            this.minScale = Math.round(this.container.clientHeight / this.size * 100) / 100;
        }

        this.scale = this.minScale;

        this.container.addEventListener("contextmenu", e => {
            e.preventDefault();
        })

        this.container.addEventListener("wheel", (e) => this.onWheel(e));
        // window.addEventListener("resize", () => this.resizeCanvas());

        this.container.addEventListener("mousedown", (event) => this.onMouseDown(event));

        this.container.addEventListener("mousemove", (event) => this.onMouseMove(event));

        this.container.addEventListener("mouseup", (event) => this.onMouseLeaveOrUp(event, "up"));
        this.container.addEventListener("mouseleave", (event) => this.onMouseLeaveOrUp(event));
    },
    generateDefaultMap(defaultColors) {
        for (let i = 0; i < defaultColors.length; i++) {
            const canvasObj = this.allCanvasObj[i];
            canvasObj.bufferContext.fillStyle = defaultColors[i];
            canvasObj.bufferContext.fillRect(0, 0, this.size, this.size);
            this.recalculateScaleAndTranslate(0, this.container.clientWidth / 2, this.container.clientHeight / 2);
            this.redraw(canvasObj);
        }
    },
    importExistingMap(imageIds) {
        console.log("importExistingMap");
        for (let i = 0; i < imageIds.length; i++) {
            const canvasObj = this.allCanvasObj[i];
            const image = window.MapGenerator1d.importedImages[canvasObj.name];

            canvasObj.bufferContext.drawImage(image, 0, 0);
            this.recalculateScaleAndTranslate(0, this.container.clientWidth / 2, this.container.clientHeight / 2);
            this.redraw(canvasObj);
        }
    },
    saveImages(imageIds) {
        for (let i = 0; i < imageIds.length; i++) {
            const image = document.getElementById(imageIds[i]);
            image.src = this.allCanvasObj[i].bufferCanvas.toDataURL("image/png");
        }
    },
    changeColor(color) {
        this.toolColor = color;
    },
    changeToolSize(toolSize) {
        this.toolSize = toolSize;
    },
    changeCanvas(name) {
        this.activeCanvasObj = this.allCanvasObj.find(canvasObj => canvasObj.name === name);
    },
    changeTool(tool) {
        this.currentTool = tool;
    },
    paint(event) {
        const rect = this.container.getBoundingClientRect();
        const mouseX = event.clientX - rect.left;
        const mouseY = event.clientY - rect.top;

        const realX = Math.floor((mouseX - this.offsetX) / this.scale);
        const realY = Math.floor((mouseY - this.offsetY) / this.scale);

        if (this.lastX === null || this.lastY === null) {
            this.lastX = realX;
            this.lastY = realY;
        }

        const ctx = this.activeCanvasObj.bufferContext;
        ctx.save();
        ctx.globalAlpha = 1;
        ctx.imageSmoothingEnabled = false;

        if (this.currentTool === "pen") {
            ctx.globalCompositeOperation = "source-over";
            ctx.fillStyle = this.toolColor;
            ctx.strokeStyle = this.toolColor;
        } else {
            ctx.globalCompositeOperation = "destination-out";
            ctx.fillStyle = "#000000";
            ctx.strokeStyle = "#000000";
        }

        this.drawInterpolatedLine(ctx, this.lastX, this.lastY, realX, realY, this.toolSize);

        ctx.restore();

        this.lastX = realX;
        this.lastY = realY;

        this.redraw(this.activeCanvasObj);
    },
    drawInterpolatedLine(ctx, x0, y0, x1, y1, radius) {
        let dx = x1 - x0;
        let dy = y1 - y0;
        let dist = Math.sqrt(dx * dx + dy * dy);

        if (dist === 0) {
            this.drawAliasedCircle(ctx, x0, y0, radius);
            return;
        }

        let step = radius / 3;
        let x = x0, y = y0;
        let stepCount = Math.ceil(dist / step);

        for (let i = 0; i < stepCount; i++) {
            this.drawAliasedCircle(ctx, Math.floor(x), Math.floor(y), radius);
            x += dx / stepCount;
            y += dy / stepCount;
        }
    },
    drawAliasedCircle(ctx, xc, yc, width) {
        ctx.beginPath();

        let x = width;
        let y = 0;
        let cd = 0;

        ctx.rect(xc - x, yc, width << 1, 1);

        while (x > y) {
            cd -= (--x) - (++y);
            if (cd < 0) cd += x++;
            ctx.rect(xc - y, yc - x, y << 1, 1);
            ctx.rect(xc - x, yc - y, x << 1, 1);
            ctx.rect(xc - x, yc + y, x << 1, 1);
            ctx.rect(xc - y, yc + x, y << 1, 1);
        }

        ctx.fill();
        ctx.closePath();
    },
    fill(event) {
        const rect = this.container.getBoundingClientRect();
        const mouseX = event.clientX - rect.left;
        const mouseY = event.clientY - rect.top;

        const realX = Math.floor((mouseX - this.offsetX) / this.scale);
        const realY = Math.floor((mouseY - this.offsetY) / this.scale);

        const ctx = this.activeCanvasObj.bufferContext;
        const canvasWidth = this.size;
        const canvasHeight = this.size;

        const imageData = ctx.getImageData(0, 0, canvasWidth, canvasHeight);
        const pixels = imageData.data;

        const startIndex = (realY * canvasWidth + realX) * 4;
        const targetColor = {
            r: pixels[startIndex],
            g: pixels[startIndex + 1],
            b: pixels[startIndex + 2],
            a: pixels[startIndex + 3]
        };

        const fillColor = hexToRGBA(this.toolColor);
        if (colorsMatch(targetColor, fillColor)) return;

        const stack = [{ x: realX, y: realY }];
        const visited = new Set();

        while (stack.length > 0) {
            const { x, y } = stack.pop();
            const index = (y * canvasWidth + x) * 4;

            if (x < 0 || y < 0 || x >= canvasWidth || y >= canvasHeight || visited.has(index)) continue;

            const pixelColor = {
                r: pixels[index],
                g: pixels[index + 1],
                b: pixels[index + 2],
                a: pixels[index + 3]
            };

            if (!colorsMatch(pixelColor, targetColor)) continue;

            pixels[index] = fillColor.r;
            pixels[index + 1] = fillColor.g;
            pixels[index + 2] = fillColor.b;
            pixels[index + 3] = fillColor.a;

            visited.add(index);

            stack.push({ x: x + 1, y });
            stack.push({ x: x - 1, y });
            stack.push({ x, y: y + 1 });
            stack.push({ x, y: y - 1 });
        }

        ctx.putImageData(imageData, 0, 0);
        this.redraw(this.activeCanvasObj);
    },
    onMouseLeaveOrUp(event, type) {
        this.isPanning = false;
        this.isDrawing = false;
        this.lastX = null;
        this.lastY = null;
        this.activeCanvasObj.canvas.style.cursor = "inherit";

        if (type === "up") {
            this.onMouseMove(event);
        } else {
            this.redraw(this.activeCanvasObj);
        }
    },
    onMouseDown(event) {
        if (event.button === 2) {
            this.isPanning = true;
            this.startX = event.clientX - this.offsetX;
            this.startY = event.clientY - this.offsetY;
            this.activeCanvasObj.canvas.style.cursor = "grabbing";
            this.redraw(this.activeCanvasObj);
        } else if (event.button === 0) {
            switch (this.currentTool) {
                case "pen":
                case "eraser":
                    this.isDrawing = true;
                    break;
                case "fill":
                    this.fill(event);
                    break;
            }

            this.onMouseMove(event);
        }
    },
    onMouseMove(event) {
        if (this.isDrawing) {
            this.paint(event);
        } else if (this.isPanning) {
            let newOffsetX = event.clientX - this.startX;
            let newOffsetY = event.clientY - this.startY;

            const scaledWidth = this.size * this.scale;
            const scaledHeight = this.size * this.scale;

            let maxOffsetX = Math.max(0, (this.container.clientWidth - scaledWidth) / 2);
            let maxOffsetY = Math.max(0, (this.container.clientHeight - scaledHeight) / 2);

            this.offsetX = Math.min(maxOffsetX, Math.max(newOffsetX, this.container.clientWidth - scaledWidth - maxOffsetX));
            this.offsetY = Math.min(maxOffsetY, Math.max(newOffsetY, this.container.clientHeight - scaledHeight - maxOffsetY));

            this.redraw(this.activeCanvasObj);
        }

        if (!this.isPanning && this.currentTool === "pen" || this.currentTool === "eraser") {
            this.drawPreviz(event);
        }
    },
    onWheel(event) {
        event.preventDefault();

        this.recalculateScaleAndTranslate(event.deltaY, event.clientX, event.clientY)

        this.allCanvasObj.forEach(canvasObj => this.redraw(canvasObj));

        this.onMouseMove(event);
    },
    recalculateScaleAndTranslate(deltaY, clientX, clientY) {
        const zoomFactor = deltaY > 0 ? 0.9 : deltaY < 0 ? 1.1 : 1;
        const rect = this.container.getBoundingClientRect();
        const mouseX = clientX - rect.left;
        const mouseY = clientY - rect.top;

        const zoomX = (mouseX - this.offsetX) / this.scale;
        const zoomY = (mouseY - this.offsetY) / this.scale;

        let newScale = Math.min(Math.max(this.scale * zoomFactor, this.minScale), this.maxScale);

        const scaledWidth = this.size * newScale;
        const scaledHeight = this.size * newScale;

        let maxOffsetX = Math.max(0, (this.container.clientWidth - scaledWidth) / 2);
        let maxOffsetY = Math.max(0, (this.container.clientHeight - scaledHeight) / 2);

        this.offsetX -= (zoomX * newScale - zoomX * this.scale);
        this.offsetY -= (zoomY * newScale - zoomY * this.scale);

        this.offsetX = Math.min(maxOffsetX, Math.max(this.offsetX, this.container.clientWidth - scaledWidth - maxOffsetX));
        this.offsetY = Math.min(maxOffsetY, Math.max(this.offsetY, this.container.clientHeight - scaledHeight - maxOffsetY));

        this.scale = newScale;
    },
    redraw({ canvas, context, bufferCanvas }) {
        context.save();
        context.setTransform(1, 0, 0, 1, 0, 0);
        context.clearRect(0, 0, canvas.width, canvas.height);

        context.translate(this.offsetX, this.offsetY);
        context.scale(this.scale, this.scale);

        context.drawImage(bufferCanvas, 0, 0);
        context.restore();
    },
    drawPreviz(event) {
        const rect = this.container.getBoundingClientRect();
        const mouseX = event.clientX - rect.left;
        const mouseY = event.clientY - rect.top;

        this.redraw(this.activeCanvasObj);
        this.activeCanvasObj.context.save();
        this.activeCanvasObj.context.translate(this.offsetX, this.offsetY);
        this.activeCanvasObj.context.scale(this.scale, this.scale);
        this.activeCanvasObj.context.globalAlpha = 0.75;

        if (this.currentTool === "pen") {
            this.activeCanvasObj.context.fillStyle = this.toolColor;
        } else {
            this.activeCanvasObj.context.fillStyle = "#00000000";
        }

        this.activeCanvasObj.context.strokeStyle = "#000000";
        this.activeCanvasObj.context.lineWidth = 1;
        this.activeCanvasObj.context.beginPath();
        this.activeCanvasObj.context.arc(
            (mouseX - this.offsetX) / this.scale,
            (mouseY - this.offsetY) / this.scale,
            this.toolSize,
            0,
            Math.PI * 2
        );
        this.activeCanvasObj.context.stroke();
        this.activeCanvasObj.context.fill();
        this.activeCanvasObj.context.restore();
    }
};

function colorsMatch(color1, color2) {
    return color1.r === color2.r && color1.g === color2.g && color1.b === color2.b && color1.a === color2.a;
}

function hexToRGBA(hex) {
    if (hex.startsWith("#")) {
        hex = hex.slice(1);
    }

    let r, g, b, a = 255;

    if (hex.length === 8) {
        r = parseInt(hex.substring(0, 2), 16);
        g = parseInt(hex.substring(2, 4), 16);
        b = parseInt(hex.substring(4, 6), 16);
        a = parseInt(hex.substring(6, 8), 16);
    } else if (hex.length === 6) {
        r = parseInt(hex.substring(0, 2), 16);
        g = parseInt(hex.substring(2, 4), 16);
        b = parseInt(hex.substring(4, 6), 16);
    } else {
        return { r: 0, g: 0, b: 0, a: 255 };
    }

    return { r, g, b, a };
}

