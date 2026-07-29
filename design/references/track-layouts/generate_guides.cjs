const fs = require("fs");
const path = require("path");
const sharp = require(process.argv[2]);

const root = process.cwd();
const sourceDirectory = path.join(root, "Assets/Resources/Configs/Tracks");
const outputDirectory = path.join(root, "design/references/track-layouts/guides");
const trackIds = [
  "suzuka_sushi",
  "shanghai_dim_sum",
  "nurburgring_bier",
  "monza_pasta",
  "indianapolis_burger",
  "nurburgring_24h_endurance",
  "le_mans_old_mulsanne",
];

fs.mkdirSync(outputDirectory, { recursive: true });

async function main() {
  for (const trackId of trackIds) {
    const config = JSON.parse(
      fs.readFileSync(path.join(sourceDirectory, `${trackId}.json`), "utf8"),
    );
    const points = config.cells.map((cell) => [
      96 + cell.position.x * 1728,
      1026 - cell.position.y * 972,
    ]);
    const pathData =
      points
        .map(
          (point, index) =>
            `${index === 0 ? "M" : "L"}${point[0].toFixed(1)} ${point[1].toFixed(1)}`,
        )
        .join(" ") + " Z";
    const [startX, startY] = points[0];
    const svg = `
      <svg width="1920" height="1080" xmlns="http://www.w3.org/2000/svg">
        <rect width="1920" height="1080" fill="#f5f1e8"/>
        <path d="${pathData}" fill="none" stroke="#151515" stroke-width="34"
              stroke-linejoin="round" stroke-linecap="round"/>
        <path d="${pathData}" fill="none" stroke="#f2c94c" stroke-width="4"
              stroke-linejoin="round"/>
        <circle cx="${startX}" cy="${startY}" r="13" fill="#2ecc71"/>
      </svg>`;
    await sharp(Buffer.from(svg))
      .png()
      .toFile(path.join(outputDirectory, `${trackId}_guide.png`));
  }
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
