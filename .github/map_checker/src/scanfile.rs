use log::{trace, debug};
use std::{fs, io};
use std::path::Path;
use std::collections::HashMap;

use crate::structs::{Matcher, Match};

pub fn scan_file(file_path: &Path, matchers_blacklist: &Vec<Matcher>, matchers_whitelist: &Vec<Matcher>) -> Result<Vec<Match>, io::Error> {
    let mut matches: Vec<Match> = Vec::new();

    let bl: Vec<&Matcher> = matchers_blacklist.iter()
        .filter(|matcher| {
            matcher.file_paths.is_empty() || matcher.file_paths.iter().any(|p| file_path.ends_with(p))
        })
        .collect();
    let wl: Vec<&Matcher> = matchers_whitelist.iter()
        .filter(|matcher| {
            matcher.file_paths.is_empty() || matcher.file_paths.iter().any(|p| file_path.ends_with(p))
        })
        .collect();

    // Read the file content
    debug!("Scanning file: {}", file_path.display());
    let content = fs::read_to_string(file_path)?;

    for bl_matcher in bl {
        trace!("Running BL matcher: {}", bl_matcher.original_pattern);
        for capture in bl_matcher.pattern.find_iter(&content) {
            let mut matched_text = capture.as_str().to_string();

            // Remove forcibly added prefix and suffix in config reading step.
            if let Some(s) = matched_text.strip_prefix("proto: ") {matched_text = s.to_string()};
            if let Some(s) = matched_text.strip_suffix("\n")   {matched_text = s.to_string()};
            trace!("MATCH! {}", matched_text);

            // Check if the match is allowed by the whitelist
            let is_whitelisted = wl.iter().any(|wl_matcher| {
                let did_match = wl_matcher.pattern.is_match(&matched_text);
                trace!("Running WL matcher: {} on {} (Matched: {})", wl_matcher.pattern.as_str(), matched_text, did_match);
                did_match
            });

            trace!("Matched {} (matched by: {}, whitelisted: {})", matched_text, bl_matcher.original_pattern, is_whitelisted);

            if !is_whitelisted {
                debug!("RETAINED MATCH! {} (matched by: {})", matched_text, bl_matcher.original_pattern);
                matches.push(Match{
                    matched_text,
                    info: format!("{} from block {}", bl_matcher.original_pattern, bl_matcher.matcher_block_id)
                });
            }
        }
    }

    let dup_matches = check_duplicate_uids(&content);
    matches.extend(dup_matches);

    Ok(matches)
}

fn check_duplicate_uids(content: &str) -> Vec<Match> {
    let mut uid_lines: HashMap<u32, Vec<usize>> = HashMap::new();
    let uid_regex = regex::Regex::new(r"uid:\s*(\d+)\b").expect("Failed to compile UID regex");

    for (i, line) in content.lines().enumerate() {
        if let Some(caps) = uid_regex.captures(line) {
            if let Some(num_str) = caps.get(1) {
                if let Ok(uid) = num_str.as_str().parse::<u32>() {
                    uid_lines.entry(uid).or_default().push(i + 1);
                }
            }
        }
    }

    let mut matches = Vec::new();
    for (uid, lines) in uid_lines {
        let count = lines.len();
        if count > 1 {
            let lines_str = lines.iter().map(|l| l.to_string()).collect::<Vec<_>>().join(", ");
            matches.push(Match {
                matched_text: format!("uid: {}", uid),
                info: format!("duplicate UID {} appears {} times at lines: {}", uid, count, lines_str),
            });
        }
    }
    matches
}
