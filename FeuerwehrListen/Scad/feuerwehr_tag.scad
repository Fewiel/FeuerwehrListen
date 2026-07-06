// ============================================================
// Feuerwehr Tag - Configurable 2-Color 3D Printable Keychain
// ============================================================
// Produces two objects for multi-color printing:
//   1. Tag body with engraved pockets (Color 2)
//   2. Text/QR code inlay pieces (Color 1)
//
// Usage:
//   - Set render_mode to "assembled" for preview
//   - Set render_mode to "body" and export as tag_body.stl
//   - Set render_mode to "inlay" and export as tag_text.stl
//   - Import both STLs at origin in slicer, assign colors
// ============================================================

include <qr.scad>

// --- Render Mode ---
/* [Render] */
render_mode = "assembled"; // [assembled:Vorschau, body:Tag Body (STL), inlay:Text/QR Inlay (STL)]

// --- Content ---
line1_text  = "FEUERWEHR";
line2_text  = "BILLERBECK";
name_text   = "P. Weitkamp";
number_text = "2040";
qr_content  = "2040";  // QR code content (based on number)

// --- Tag Dimensions (mm) ---
tag_width      = 40;    // 4cm
tag_height     = 60;    // 6cm
tag_thickness  = 1.6;
corner_radius  = 5;
engrave_depth  = 0.8;

// --- Keyring Slot ---
slot_width    = 15;
slot_height   = 3.5;
slot_corner_r = 1.75;
slot_y_offset = 4.5;   // distance from top edge to slot center

// --- Font Settings ---
font_bold   = "Liberation Sans:style=Bold";
font_normal = "Dubai:style=Bold";

size_line1  = 3.8;
size_line2  = 3.8;
size_name   = 3.5;
size_number = 5.0;

// --- Layout Y-Positions (from center, positive = up) ---
line1_y   = 20;
line2_y   = 15;
name_y    = 8;
number_y  = 3;
qr_y      = -14;

// --- QR Code Settings ---
qr_size        = 27;   // width & height in mm
qr_error_corr  = "H";  // L, M, Q, H

// --- Smoothness ---
$fn = 64;


// ============================================================
// Modules
// ============================================================

// 2D rounded rectangle centered at origin
module rounded_rect(w, h, r) {
    hull() {
        translate([ (w/2 - r),  (h/2 - r)]) circle(r);
        translate([-(w/2 - r),  (h/2 - r)]) circle(r);
        translate([ (w/2 - r), -(h/2 - r)]) circle(r);
        translate([-(w/2 - r), -(h/2 - r)]) circle(r);
    }
}

// 2D tag outline with keyring slot
module tag_outline() {
    difference() {
        rounded_rect(tag_width, tag_height, corner_radius);
        translate([0, tag_height/2 - slot_y_offset])
            rounded_rect(slot_width, slot_height, slot_corner_r);
    }
}

// Full-thickness 3D tag slab
module tag_base() {
    linear_extrude(height = tag_thickness)
        tag_outline();
}

// QR code block
module qr_block() {
    qr(qr_content,
       width            = qr_size,
       height           = qr_size,
       thickness        = engrave_depth,
       center           = true,
       error_correction = qr_error_corr);
}

// Union of all text + QR code geometry at z=0, height=engrave_depth
module all_engravings() {
    // Line 1
    translate([0, line1_y, 0])
        linear_extrude(height = engrave_depth)
            text(line1_text, size = size_line1, font = font_bold,
                 halign = "center", valign = "center");

    // Line 2
    translate([0, line2_y, 0])
        linear_extrude(height = engrave_depth)
            text(line2_text, size = size_line2, font = font_bold,
                 halign = "center", valign = "center");

    // Name
    translate([0, name_y, 0])
        linear_extrude(height = engrave_depth)
            text(name_text, size = size_name, font = font_normal,
                 halign = "center", valign = "center");

    // Number
    translate([0, number_y, 0])
        linear_extrude(height = engrave_depth)
            text(number_text, size = size_number, font = font_bold,
                 halign = "center", valign = "center");

    // QR Code
    translate([0, qr_y, 0])
        qr_block();
}

// Tag body with engraved pockets (top face)
module tag_body() {
    difference() {
        tag_base();
        // Shift engravings to top face of tag
        translate([0, 0, tag_thickness - engrave_depth])
            all_engravings();
    }
}

// Text/QR inlay pieces (clipped to tag outline)
module tag_inlay() {
    intersection() {
        // Engravings at top face position
        translate([0, 0, tag_thickness - engrave_depth])
            all_engravings();
        // Clip to tag perimeter
        tag_base();
    }
}

// Flip for print bed: decorated face at z=0
module rotated_for_print() {
    translate([0, 0, tag_thickness])
        rotate([180, 0, 0])
            children();
}


// ============================================================
// Render
// ============================================================

if (render_mode == "assembled") {
    // Preview: Tag body = Blue, Text/QR inlay = Red
    color("Blue") tag_body();
    color("Red")  tag_inlay();
}
else if (render_mode == "body") {
    // Export as tag_body.stl
    rotated_for_print()
        tag_body();
}
else if (render_mode == "inlay") {
    // Export as tag_text.stl
    rotated_for_print()
        tag_inlay();
}
