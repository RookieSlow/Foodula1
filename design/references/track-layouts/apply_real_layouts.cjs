const fs = require("fs");
const path = require("path");

const projectRoot = process.cwd();
const referenceRoot = path.join(projectRoot, "design/references/track-layouts");
const configRoot = path.join(
  projectRoot,
  "Assets/Resources/Configs/Tracks",
);

const trackSources = [
  {
    id: "suzuka_sushi",
    source: "suzuka_2005.svg",
    points: () => readSvgPath("suzuka_2005.svg", "path3610"),
    rotate: 0,
  },
  {
    id: "shanghai_dim_sum",
    source: "shanghai_gp.svg",
    points: () => readSvgPath("shanghai_gp.svg", "path3524-7"),
    rotate: 0,
  },
  {
    id: "nurburgring_bier",
    source: "nurburgring_gp.svg",
    // This SVG path contains a stray move command after the closed circuit.
    // Read only the first closed subpath so the annotation point is not
    // treated as another track node and connected back into the racing line.
    points: () => readSvgClosedPathByIndex("nurburgring_gp.svg", 7),
    rotate: 90,
  },
  {
    id: "monza_pasta",
    source: "monza_centerline.csv",
    points: () => readCsv("monza_centerline.csv"),
    rotate: 0,
  },
  {
    id: "indianapolis_burger",
    source: "indianapolis_centerline.csv",
    points: () => readCsv("indianapolis_centerline.csv"),
    rotate: 90,
  },
  {
    id: "nurburgring_24h_endurance",
    source: "nurburgring_nordschleife_2013.svg",
    points: () => readSvgClosedPathByIndex("nurburgring_nordschleife_2013.svg", 9),
    rotate: 0,
  },
  {
    id: "le_mans_old_mulsanne",
    source: "le_mans_1987_1989.png",
    points: () => leMans1987Reference,
    rotate: 0,
  },
];

// Digitized from the 1987–1989 Circuit de la Sarthe map. This is the historic
// uninterrupted Mulsanne straight layout used by the project configuration.
const leMans1987Reference = [
  [0.10, 0.34], [0.15, 0.24], [0.26, 0.20], [0.39, 0.20],
  [0.51, 0.21], [0.64, 0.22], [0.76, 0.25], [0.86, 0.31],
  [0.92, 0.39], [0.94, 0.49], [0.93, 0.60], [0.89, 0.68],
  [0.84, 0.74], [0.78, 0.77], [0.71, 0.76], [0.65, 0.72],
  [0.59, 0.68], [0.53, 0.66], [0.47, 0.68], [0.42, 0.74],
  [0.36, 0.78], [0.30, 0.76], [0.27, 0.70], [0.26, 0.62],
  [0.23, 0.57], [0.18, 0.55], [0.14, 0.50], [0.11, 0.43],
];

function readCsv(fileName) {
  return fs
    .readFileSync(path.join(referenceRoot, fileName), "utf8")
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("#"))
    .map((line) => line.split(/[;,\s]+/).map(Number))
    .filter((values) => values.length >= 2 && values.every(Number.isFinite))
    .map((values) => [values[0], values[1]]);
}

function readSvgPath(fileName, id) {
  const tags = extractPathTags(fileName);
  const tag = tags.find((candidate) =>
    new RegExp(`\\bid=["']${escapeRegExp(id)}["']`).test(candidate),
  );
  if (!tag) {
    throw new Error(`SVG path '${id}' not found in ${fileName}`);
  }
  return sampleSvgPath(readAttribute(tag, "d"));
}

function readSvgPathByIndex(fileName, index) {
  const tags = extractPathTags(fileName);
  if (!tags[index]) {
    throw new Error(`SVG path index ${index} not found in ${fileName}`);
  }
  return sampleSvgPath(readAttribute(tags[index], "d"));
}

function readSvgClosedPathByIndex(fileName, index) {
  const tags = extractPathTags(fileName);
  if (!tags[index]) {
    throw new Error(`SVG path index ${index} not found in ${fileName}`);
  }
  const pathData = readAttribute(tags[index], "d");
  const closedPath = pathData.match(/^[\s\S]*?\b[zZ]\b/);
  if (!closedPath) {
    throw new Error(`Closed SVG path index ${index} not found in ${fileName}`);
  }
  return sampleSvgPath(closedPath[0]);
}

function extractPathTags(fileName) {
  const svg = fs.readFileSync(path.join(referenceRoot, fileName), "utf8");
  return [...svg.matchAll(/<path\b[^>]*>/gis)].map((match) => match[0]);
}

function readAttribute(tag, attribute) {
  const match = tag.match(
    new RegExp(`\\b${attribute}\\s*=\\s*(?:"([^"]*)"|'([^']*)')`, "is"),
  );
  if (!match) {
    throw new Error(`Attribute '${attribute}' missing from SVG path`);
  }
  return match[1] ?? match[2];
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function sampleSvgPath(pathData) {
  const tokens = [
    ...pathData.matchAll(/[a-zA-Z]|[-+]?(?:\d*\.)?\d+(?:[eE][-+]?\d+)?/g),
  ].map((match) => match[0]);
  const points = [];
  let cursor = 0;
  let command = null;
  let current = [0, 0];
  let start = [0, 0];
  let previousControl = null;

  const isCommand = (token) => /^[a-zA-Z]$/.test(token);
  const hasNumber = () => cursor < tokens.length && !isCommand(tokens[cursor]);
  const number = () => Number(tokens[cursor++]);
  const point = (relative) => {
    const value = [number(), number()];
    return relative
      ? [current[0] + value[0], current[1] + value[1]]
      : value;
  };
  const appendLine = (end) => {
    points.push(end);
    current = end;
    previousControl = null;
  };
  const appendCurve = (control1, control2, end) => {
    const from = current;
    for (let step = 1; step <= 12; step += 1) {
      const t = step / 12;
      const u = 1 - t;
      points.push([
        u ** 3 * from[0] +
          3 * u ** 2 * t * control1[0] +
          3 * u * t ** 2 * control2[0] +
          t ** 3 * end[0],
        u ** 3 * from[1] +
          3 * u ** 2 * t * control1[1] +
          3 * u * t ** 2 * control2[1] +
          t ** 3 * end[1],
      ]);
    }
    current = end;
    previousControl = control2;
  };

  while (cursor < tokens.length) {
    if (isCommand(tokens[cursor])) {
      command = tokens[cursor++];
    }
    if (!command) {
      throw new Error("SVG path begins without a command");
    }

    const relative = command === command.toLowerCase();
    const upper = command.toUpperCase();
    if (upper === "M") {
      current = point(relative);
      start = current;
      points.push(current);
      previousControl = null;
      command = relative ? "l" : "L";
    } else if (upper === "L") {
      while (hasNumber()) appendLine(point(relative));
    } else if (upper === "H") {
      while (hasNumber()) {
        const x = number();
        appendLine([relative ? current[0] + x : x, current[1]]);
      }
    } else if (upper === "V") {
      while (hasNumber()) {
        const y = number();
        appendLine([current[0], relative ? current[1] + y : y]);
      }
    } else if (upper === "C") {
      while (hasNumber()) {
        const control1 = point(relative);
        const control2 = point(relative);
        const end = point(relative);
        appendCurve(control1, control2, end);
      }
    } else if (upper === "S") {
      while (hasNumber()) {
        const control1 = previousControl
          ? [
              2 * current[0] - previousControl[0],
              2 * current[1] - previousControl[1],
            ]
          : current;
        const control2 = point(relative);
        const end = point(relative);
        appendCurve(control1, control2, end);
      }
    } else if (upper === "Q") {
      while (hasNumber()) {
        const control = point(relative);
        const end = point(relative);
        appendCurve(
          [
            current[0] + (2 / 3) * (control[0] - current[0]),
            current[1] + (2 / 3) * (control[1] - current[1]),
          ],
          [
            end[0] + (2 / 3) * (control[0] - end[0]),
            end[1] + (2 / 3) * (control[1] - end[1]),
          ],
          end,
        );
      }
    } else if (upper === "Z") {
      appendLine(start);
      command = null;
    } else {
      throw new Error(`Unsupported SVG command '${command}'`);
    }
  }
  return points;
}

function rotate(points, degrees) {
  const radians = (degrees * Math.PI) / 180;
  const cosine = Math.cos(radians);
  const sine = Math.sin(radians);
  return points.map(([x, y]) => [
    x * cosine - y * sine,
    x * sine + y * cosine,
  ]);
}

function normalize(points) {
  const xs = points.map((point) => point[0]);
  const ys = points.map((point) => point[1]);
  const minX = Math.min(...xs);
  const maxX = Math.max(...xs);
  const minY = Math.min(...ys);
  const maxY = Math.max(...ys);
  const width = Math.max(maxX - minX, Number.EPSILON);
  const height = Math.max(maxY - minY, Number.EPSILON);
  const scale = Math.min(0.88 / width, 0.78 / height);
  const usedWidth = width * scale;
  const usedHeight = height * scale;
  const left = (1 - usedWidth) / 2;
  const bottom = (1 - usedHeight) / 2;
  return points.map(([x, y]) => [
    left + (x - minX) * scale,
    bottom + (maxY - y) * scale,
  ]);
}

function resampleClosed(points, count) {
  const cleaned = points.filter(
    (point, index) =>
      index === 0 ||
      Math.hypot(
        point[0] - points[index - 1][0],
        point[1] - points[index - 1][1],
      ) > 1e-8,
  );
  if (
    Math.hypot(
      cleaned[0][0] - cleaned[cleaned.length - 1][0],
      cleaned[0][1] - cleaned[cleaned.length - 1][1],
    ) > 1e-8
  ) {
    cleaned.push(cleaned[0]);
  }

  const cumulative = [0];
  for (let index = 1; index < cleaned.length; index += 1) {
    cumulative.push(
      cumulative[index - 1] +
        Math.hypot(
          cleaned[index][0] - cleaned[index - 1][0],
          cleaned[index][1] - cleaned[index - 1][1],
        ),
    );
  }
  const total = cumulative[cumulative.length - 1];
  const result = [];
  let segment = 1;
  for (let index = 0; index < count; index += 1) {
    const target = (index / count) * total;
    while (segment < cumulative.length - 1 && cumulative[segment] < target) {
      segment += 1;
    }
    const segmentStart = cumulative[segment - 1];
    const segmentLength = cumulative[segment] - segmentStart || 1;
    const t = (target - segmentStart) / segmentLength;
    result.push([
      cleaned[segment - 1][0] +
        (cleaned[segment][0] - cleaned[segment - 1][0]) * t,
      cleaned[segment - 1][1] +
        (cleaned[segment][1] - cleaned[segment - 1][1]) * t,
    ]);
  }
  return result;
}

function countSelfIntersections(points) {
  const orientation = (a, b, c) =>
    Math.sign(
      (b[0] - a[0]) * (c[1] - a[1]) -
        (b[1] - a[1]) * (c[0] - a[0]),
    );
  let intersections = 0;
  for (let first = 0; first < points.length; first += 1) {
    const firstNext = (first + 1) % points.length;
    for (let second = first + 2; second < points.length; second += 1) {
      const secondNext = (second + 1) % points.length;
      if (first === secondNext || firstNext === second) continue;
      const a = points[first];
      const b = points[firstNext];
      const c = points[second];
      const d = points[secondNext];
      if (
        orientation(a, b, c) !== orientation(a, b, d) &&
        orientation(c, d, a) !== orientation(c, d, b)
      ) {
        intersections += 1;
      }
    }
  }
  return intersections;
}

const requestedTrackIds = new Set(process.argv.slice(2));

for (const track of trackSources) {
  if (requestedTrackIds.size > 0 && !requestedTrackIds.has(track.id)) {
    continue;
  }
  const configPath = path.join(configRoot, `${track.id}.json`);
  const config = JSON.parse(fs.readFileSync(configPath, "utf8"));
  const sourcePoints = rotate(track.points(), track.rotate);
  const normalized = normalize(sourcePoints);
  const sampled = resampleClosed(normalized, config.cells.length);
  const normalizedForValidation =
    normalized.length > 1 &&
    Math.hypot(
      normalized[0][0] - normalized[normalized.length - 1][0],
      normalized[0][1] - normalized[normalized.length - 1][1],
    ) <= 1e-8
      ? normalized.slice(0, -1)
      : normalized;
  const sourceCrossings = countSelfIntersections(normalizedForValidation);
  const crossings = countSelfIntersections(sampled);
  if (
    (track.id === "nurburgring_bier" ||
      track.id === "nurburgring_24h_endurance") &&
    (sourceCrossings !== 0 || crossings !== 0)
  ) {
    throw new Error(
      `${track.id} source produced ${sourceCrossings} source crossings and ` +
        `${crossings} sampled crossings`,
    );
  }

  config.cells.forEach((cell, index) => {
    cell.position = {
      x: Number(sampled[index][0].toFixed(6)),
      y: Number(sampled[index][1].toFixed(6)),
    };
  });
  fs.writeFileSync(configPath, `${JSON.stringify(config, null, 2)}\n`);
  console.log(
    `${track.id}: ${track.source}, ${sourcePoints.length} source points, ` +
      `${sourceCrossings} source crossings -> ${sampled.length} cells, ` +
      `${crossings} sampled crossings`,
  );
}
