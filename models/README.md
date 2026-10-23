# Wake-word model bundles

No acoustic model is committed or downloaded automatically. A model may be used only after its provenance, redistribution terms, exact SHA-256 hashes, and model-specific tokenization of **Hey Computer** are recorded in a bundle manifest and accepted by the validator.

## Current decision

The leading English candidate is `sherpa-onnx-kws-zipformer-gigaspeech-3.3M-2024-01-01`, but it is **not approved for bundling or automatic download**. The official sherpa-onnx documentation identifies it as the English open-vocabulary KWS model and its archive reportedly declares Apache-2.0, but the upstream public clarification about the weights, vocabulary assets, training-data implications, commercial use, and redistribution remains unresolved. See [licensing-audit.md](licensing-audit.md).

This distinction is deliberate: sherpa-onnx framework code and NuGet packages may be Apache-2.0 while a separately published model bundle still needs its own attributable rights record. Until the release asset or its publisher provides an unambiguous model-specific license covering every redistributed file, development builds must require a user-supplied model bundle and public packages must contain no acoustic model.

Each model bundle is a separate directory containing:

- `manifest.json`
- `encoder.onnx`
- `decoder.onnx`
- `joiner.onnx`
- `tokens.txt`

Copy `example.manifest.json` into the model directory, replace every placeholder with verified source and license information, and calculate the hashes from the exact files. The initial allowlist accepts Apache-2.0, MIT, BSD-2-Clause, BSD-3-Clause, and CC0-1.0. A different model license requires explicit review and an injected allowlist; setting `redistributionAllowed` to true is an assertion that must be supported by the model's actual terms.

The `keywordTokens` value is not ordinary text. It must use the token spelling expected by the selected model vocabulary. Model selection, tokenization, thresholds, false-accept testing, and false-reject testing remain release-gated work.

For the English GigaSpeech candidate, create a raw input containing `HEY COMPUTER`, then run the model's own BPE conversion rather than guessing the pieces:

```text
sherpa-onnx-cli text2token --tokens tokens.txt --tokens-type bpe --bpe-model bpe.model keywords_raw.txt keywords.txt
```

The generated token sequence, boosting score, and trigger threshold must be recorded in the final manifest only after the exact files pass provenance review. The official KWS format permits an optional boosting score such as `:1.5` and a trigger threshold such as `#0.35`; those values are tuning inputs, not defaults that may be copied without measurement. Follow [wake-evaluation-protocol.md](wake-evaluation-protocol.md) before promoting a model.
