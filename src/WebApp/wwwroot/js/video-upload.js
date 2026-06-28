const CHUNK_SIZE = 5 * 1024 * 1024; // 5 MB

// ✅ ДОБАВЬТЕ: Обработчик submit формы
document.addEventListener('DOMContentLoaded', function () {
    const uploadForm = document.getElementById('uploadForm');
    const progressContainer = document.getElementById('progressContainer');
    const resultMessage = document.getElementById('resultMessage');

    if (uploadForm) {
        uploadForm.addEventListener('submit', async function (e) {
            e.preventDefault();

            const fileInput = document.getElementById('videoFile');
            const file = fileInput.files[0];

            if (!file) {
                alert('Пожалуйста, выберите видеофайл');
                return;
            }

            if (file.size > window.AppConfig.MaxFileSizeBytes) {
                alert(`Файл слишком большой. Максимальный размер: ${window.AppConfig.MaxFileSizeMB} MB`);
                return;
            }

            progressContainer.style.display = 'block';
            resultMessage.innerHTML = '';

            try {
                await uploadVideo(file);
            } catch (error) {
                console.error('Upload error:', error);
                resultMessage.innerHTML = `
                    <div class="alert alert-danger">
                        <i class="bi bi-exclamation-triangle"></i>
                        Ошибка загрузки: ${error.message}
                    </div>
                `;
                progressContainer.style.display = 'none';
            }
        });
    }
});

async function uploadVideo(file) {
    const totalChunks = Math.ceil(file.size / CHUNK_SIZE);

    updateProgress(0, 0, totalChunks, 'Инициализация...');

    const initResponse = await fetch('/Videos/InitUpload', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            fileName: file.name,
            fileSize: file.size,
            chunkSize: CHUNK_SIZE,
            totalChunks: totalChunks
        })
    });

    if (!initResponse.ok) {
        const errorData = await initResponse.json();
        throw new Error(errorData.error || 'Failed to initialize upload');
    }

    const { videoId, uploadUrls } = await initResponse.json();
    console.log(`✅ Initialized upload for video ${videoId}`);

    for (let i = 0; i < totalChunks; i++) {
        const chunk = file.slice(i * CHUNK_SIZE, (i + 1) * CHUNK_SIZE);
        const uploadUrl = uploadUrls[i].uploadUrl;

        const response = await fetch(uploadUrl, {
            method: 'PUT',
            body: chunk,
            headers: {
                'Content-Type': 'application/octet-stream'
            }
        });

        if (!response.ok) {
            throw new Error(`Failed to upload chunk ${i}`);
        }

        const percentage = Math.round(((i + 1) / totalChunks) * 100);
        updateProgress(percentage, i + 1, totalChunks, 'Загрузка...');
    }

    updateProgress(100, totalChunks, totalChunks, 'Завершение загрузки...');

    const completeResponse = await fetch('/Videos/CompleteUpload', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            videoId: videoId,
            fileName: file.name,
            totalChunks: totalChunks
        })
    });

    if (!completeResponse.ok) {
        const errorData = await completeResponse.json();
        throw new Error(errorData.error || 'Failed to complete upload');
    }

    console.log('✅ Upload completed!');

    const resultMessage = document.getElementById('resultMessage');
    resultMessage.innerHTML = `
        <div class="alert alert-success">
            <i class="bi bi-check-circle"></i>
            Видео загружено! Перенаправление на страницу обработки...
        </div>
    `;

    // ✅ ИЗМЕНЕНО: Перенаправляем на страницу Watch с прогрессом обработки
    setTimeout(() => {
        window.location.href = `/Videos/Watch/${videoId}`;
    }, 1500);
}

function updateProgress(percentage, uploadedChunks, totalChunks, status) {
    const progressBar = document.getElementById('progressBar');
    const progressText = document.getElementById('progressText');

    if (progressBar) {
        progressBar.style.width = `${percentage}%`;
        progressBar.textContent = `${percentage}%`;
    }

    if (progressText) {
        progressText.textContent = `${status} (${uploadedChunks}/${totalChunks})`;
    }
}