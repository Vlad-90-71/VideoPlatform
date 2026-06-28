#!/bin/sh
set -e

echo "🔄 Waiting for MinIO to be ready..."
until mc alias set vpminio https://minio:9000 minio_admin minio_secret --insecure 2>/dev/null; do
    echo "   MinIO is not ready yet, waiting..."
    sleep 2
done

echo "✅ MinIO is ready!"

echo "📦 Creating buckets..."
mc mb vpminio/video-storage --ignore-existing --insecure
mc mb vpminio/video-hls --ignore-existing --insecure
echo "✅ Buckets created"

echo "🔐 Setting public read policy for video-hls (for HLS streaming)..."
cat > /tmp/hls-policy.json << 'INNEREOF'
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {"AWS": ["*"]},
            "Action": ["s3:GetObject"],
            "Resource": ["arn:aws:s3:::video-hls/*"]
        }
    ]
}
INNEREOF

mc anonymous set-json /tmp/hls-policy.json vpminio/video-hls --insecure
echo "✅ Public read policy set for video-hls"

echo "🔐 Setting policy for video-storage (for presigned uploads)..."
cat > /tmp/storage-policy.json << 'INNEREOF'
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Principal": {"AWS": ["*"]},
            "Action": ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"],
            "Resource": ["arn:aws:s3:::video-storage/*"]
        }
    ]
}
INNEREOF

mc anonymous set-json /tmp/storage-policy.json vpminio/video-storage --insecure
echo "✅ Policy set for video-storage"

echo "🎉 MinIO initialization completed successfully!"
