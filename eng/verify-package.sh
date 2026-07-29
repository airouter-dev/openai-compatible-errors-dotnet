#!/usr/bin/env bash
set -euo pipefail

package_directory="${1:-artifacts/packages}"
expected_id="AiRouter.OpenAICompatibleErrors"
expected_version="${PACKAGE_VERSION:-0.1.0}"
expected_prefix="${expected_id}.${expected_version}"
nupkg="${package_directory}/${expected_prefix}.nupkg"
snupkg="${package_directory}/${expected_prefix}.snupkg"

for command_name in grep sha256sum unzip; do
  if ! command -v "${command_name}" >/dev/null 2>&1; then
    echo "Required command is unavailable: ${command_name}" >&2
    exit 1
  fi
done

if [[ ! -f "${nupkg}" || ! -f "${snupkg}" ]]; then
  echo "Expected package pair not found in ${package_directory}" >&2
  exit 1
fi

temporary_directory="$(mktemp -d)"
trap 'rm -rf -- "${temporary_directory}"' EXIT

unzip -Z1 "${nupkg}" | LC_ALL=C sort >"${temporary_directory}/nupkg-files"
unexpected_files="$({
  grep -Ev \
    '^(_rels/\.rels|\[Content_Types\]\.xml|AiRouter\.OpenAICompatibleErrors\.nuspec|README\.md|package-icon\.png|lib/net8\.0/AiRouter\.OpenAICompatibleErrors\.(dll|xml)|lib/netstandard2\.0/AiRouter\.OpenAICompatibleErrors\.(dll|xml)|package/services/metadata/core-properties/[0-9a-f]+\.psmdcp)$' \
    "${temporary_directory}/nupkg-files" || true
})"

if [[ -n "${unexpected_files}" ]]; then
  echo "Unexpected files in nupkg:" >&2
  echo "${unexpected_files}" >&2
  exit 1
fi

if [[ "$(wc -l <"${temporary_directory}/nupkg-files")" -ne 10 ]]; then
  echo "The nupkg file set is incomplete or duplicated." >&2
  exit 1
fi

unzip -p "${nupkg}" "${expected_id}.nuspec" >"${temporary_directory}/package.nuspec"
for required_pattern in \
  "<id>${expected_id}</id>" \
  "<version>${expected_version}</version>" \
  '<license type="expression">MIT</license>' \
  '<readme>README.md</readme>' \
  '<icon>package-icon.png</icon>' \
  '<projectUrl>https://github.com/airouter-dev/openai-compatible-errors-dotnet</projectUrl>' \
  '<repository type="git" url="https://github.com/airouter-dev/openai-compatible-errors-dotnet.git"' \
  '<group targetFramework="net8.0" />' \
  '<group targetFramework=".NETStandard2.0" />'; do
  if ! grep -Fq "${required_pattern}" "${temporary_directory}/package.nuspec"; then
    echo "Missing nuspec metadata: ${required_pattern}" >&2
    exit 1
  fi
done

if grep -Eq '<dependency[[:space:]]' "${temporary_directory}/package.nuspec"; then
  echo "Runtime dependency unexpectedly present in the nupkg." >&2
  exit 1
fi

unzip -p "${nupkg}" README.md >"${temporary_directory}/README.md"
if [[ "$(sha256sum README.md | cut -d' ' -f1)" != "$(sha256sum "${temporary_directory}/README.md" | cut -d' ' -f1)" ]]; then
  echo "Packed README differs from the repository README." >&2
  exit 1
fi

unzip -Z1 "${snupkg}" | LC_ALL=C sort >"${temporary_directory}/snupkg-files"
unexpected_symbol_files="$({
  grep -Ev \
    '^(_rels/\.rels|\[Content_Types\]\.xml|AiRouter\.OpenAICompatibleErrors\.nuspec|lib/net8\.0/AiRouter\.OpenAICompatibleErrors\.pdb|lib/netstandard2\.0/AiRouter\.OpenAICompatibleErrors\.pdb|package/services/metadata/core-properties/[0-9a-f]+\.psmdcp)$' \
    "${temporary_directory}/snupkg-files" || true
})"

if [[ -n "${unexpected_symbol_files}" || "$(wc -l <"${temporary_directory}/snupkg-files")" -ne 6 ]]; then
  echo "The snupkg contains an unexpected file set." >&2
  echo "${unexpected_symbol_files}" >&2
  exit 1
fi

sha256sum "${nupkg}" "${snupkg}"
echo "Package metadata and file allowlists verified."
