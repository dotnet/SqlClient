#!/usr/bin/env node

/**
 * Render a Mermaid diagram (.mmd file) to ASCII art or SVG using beautiful-mermaid.
 *
 * Usage:
 *   node render.mjs <input.mmd> [--format ascii|svg] [--output <file>]
 *
 * Examples:
 *   node render.mjs diagram.mmd                          # ASCII to stdout
 *   node render.mjs diagram.mmd --format svg             # SVG to stdout
 *   node render.mjs diagram.mmd --output diagram.txt     # ASCII to file
 */

import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { execSync } from 'node:child_process';

function parseArgs(args) {
  const parsed = { format: 'ascii', output: null, input: null };
  let i = 0;
  while (i < args.length) {
    if (args[i] === '--format' && i + 1 < args.length) {
      parsed.format = args[++i];
    } else if (args[i] === '--output' && i + 1 < args.length) {
      parsed.output = args[++i];
    } else if (!args[i].startsWith('--')) {
      parsed.input = args[i];
    }
    i++;
  }
  return parsed;
}

async function main() {
  const args = parseArgs(process.argv.slice(2));

  if (!args.input) {
    console.error('Usage: render.mjs <input.mmd> [--format ascii|svg] [--output <file>]');
    process.exit(1);
  }

  if (!['ascii', 'svg'].includes(args.format)) {
    console.error('Invalid format: "' + args.format + '". Must be "ascii" or "svg".');
    process.exit(1);
  }

  const inputPath = resolve(args.input);
  let source;
  try {
    source = readFileSync(inputPath, 'utf-8').trim();
  } catch (err) {
    console.error('Failed to read input file: ' + inputPath);
    console.error(err.message);
    process.exit(1);
  }

  if (!source) {
    console.error('Input file is empty.');
    process.exit(1);
  }

  let mod;
  try {
    mod = await import('beautiful-mermaid');
  } catch {
    console.error('beautiful-mermaid not found. Installing...');
    try {
      execSync('npm install beautiful-mermaid', { stdio: 'inherit' });
      mod = await import('beautiful-mermaid');
    } catch (installErr) {
      console.error('Failed to install beautiful-mermaid. Please install manually:');
      console.error('  npm install -g beautiful-mermaid');
      process.exit(1);
    }
  }

  const { renderMermaidAscii, renderMermaidSVG } = mod;

  let result;
  try {
    if (args.format === 'ascii') {
      result = renderMermaidAscii(source);
    } else {
      result = renderMermaidSVG(source);
    }
  } catch (renderErr) {
    console.error('Mermaid rendering failed:');
    console.error(renderErr.message);
    process.exit(1);
  }

  if (args.output) {
    const outputPath = resolve(args.output);
    writeFileSync(outputPath, result, 'utf-8');
    console.error('Written to ' + outputPath);
  } else {
    process.stdout.write(result);
  }
}

main();
