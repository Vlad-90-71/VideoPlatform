const CHUNK_SIZE = 5 * 1024 * 1024; // 5 MB

// ✅ ДОБАВЬТЕ: Обработчик submit формы
document.addEventListener('DOMContentLoaded', function () {
    const uploadForm = document.getElementById('uploadForm');
    const progressContainer = document.getElementById('progressContainer');
    const resultMessage = document.getElementById('resultMessage');

    if (uploadForm) {
        uploadForm.addEventListener('submit', async function (e) {
            e.preventDefault(); // Предотвращаем стандартное поведение формы

            const fileInput = document.getElementById('videoFile');
            const file = fileInput.files[0];

            if (!file) {
                alert('Пожалуйста, выберите видеофайл');
                return;
            }

            // Проверка размера файла
            if (file.size > window.AppConfig.MaxFileSizeBytes) {
                alert(`Файл слишком большой. Максимальный размер: ${window.AppConfig.MaxFileSizeMB} MB`);
                return;
            }

            // Показываем прогресс-бар
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

    // ✅ Шаг 1: Инициализация загрузки и получение presigned URLs
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

    // ✅ Шаг 2: Загрузка чанков НАПРЯМУЮ в MinIO
    for (let i = 0; i < totalChunks; i++) {
        const chunk = file.slice(i * CHUNK_SIZE, (i + 1) * CHUNK_SIZE);
        const uploadUrl = uploadUrls[i].uploadUrl;

        // Прямая загрузка в MinIO через presigned URL
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

        // Обновляем прогресс
        const percentage = Math.round(((i + 1) / totalChunks) * 100);
        updateProgress(percentage, i + 1, totalChunks, 'Загрузка...');
    }

    // ✅ Шаг 3: Завершение загрузки
    updateProgress(100, totalChunks, totalChunks, 'Завершение...');

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
            Видео успешно загружено!
        </div>
    `;

    // Перенаправление через 2 секунды
    setTimeout(() => {
        window.location.href = '/Videos/Index';
    }, 2000);
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